using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>Options for <see cref="TerrainMesher"/>.</summary>
    public sealed class TerrainOptions
    {
        /// <summary>Decimation in source samples (power of two; rounded down otherwise, clamped to the area's share
        /// of the source grid). <see cref="Streaming.StreamingConfig.TerrainStep"/> picks it per ring.</summary>
        public int Step = 1;

        /// <summary>Depth of the skirts hung from all four edges to hide LOD cracks: below 0 automatic
        /// (<see cref="AutoSkirtDepthM"/>), 0 no skirts.</summary>
        public float SkirtDepthM = -1f;

        /// <summary>Palette season for the biome colours.</summary>
        public Season Season = BiomePalette.DefaultSeason;

        /// <summary>Blend steep ground into the biome's slope and rock colours.</summary>
        public bool SlopeColours = true;

        /// <summary>
        /// The detail look (docs/W2_DETAIL_CONTRACT.md §1.6, §5): world-fixed colour patches (dry grass, canopy mottling
        /// on forest, rock outcrops breaking through steep ground), the field patchwork of cropland (paddy, mustard,
        /// wheat, stubble by season), micro-relief in the normals (hummocky ground; fades out on coarse grids) and UV0
        /// material channels with baked AO. Off: the plain W1 biome palette with exact central-difference normals.
        /// </summary>
        public bool Detail = true;

        /// <summary>Strength of the micro-relief in the normals (0 none; 1 the default hummocks).</summary>
        public float MicroRelief = 1f;

        /// <summary>Draw the road cut and fill (<see cref="TerrainCarve"/>: the terrain carved to meet level roads, refined
        /// cells around them) on detail tiles, as <see cref="TileHeightSampler"/> samples it. Off: the natural grid only.</summary>
        public bool Carve = true;

        /// <summary>With <see cref="Carve"/>: the retaining walls (gabion and dry stone) and the parapets along big drops
        /// that the carve builds (<see cref="TerrainCarve.AppendWalls"/>), in the terrain mesh.</summary>
        public bool Walls = true;

        /// <summary>Decoded edge neighbours of the source tile: border normals (and slope colours) then match the
        /// neighbouring tiles' exactly. Give the same neighbours to <see cref="TileHeightSampler.ForArea(TileData,
        /// TileId, int, TileNeighbours)"/> so overlays shade alike. None: one-sided differences on the border.</summary>
        public TileNeighbours Neighbours;

        /// <summary>Automatic skirt depth for a grid spacing: twice the spacing, at least 4 m and at most 600 m
        /// (cracks between neighbouring LODs are at most a fraction of the coarser spacing's relief).</summary>
        public static float AutoSkirtDepthM(double cellM)
        {
            return (float)Math.Max(4.0, Math.Min(2.0 * cellM, 600.0));
        }
    }

    /// <summary>
    /// Terrain meshes from HGHT (ARCHITECTURE.md 7.3): the <see cref="TerrainGrid"/> of an area (the source grid
    /// cropped to the area and decimated by <see cref="TerrainOptions.Step"/>), every quad split along its
    /// (i, j)-(i+1, j+1) diagonal, smooth normals from central differences on the source grid at the step spacing
    /// (across the source tile's border into <see cref="TerrainOptions.Neighbours"/>, one-sided where a neighbour is
    /// missing), biome colours from the nearest BIOM sample through
    /// <see cref="BiomePalette"/> with the detail look of <see cref="TerrainLook"/> (patches, field patchwork, outcrops,
    /// micro-relief normals, UV0 channels and AO; all world-fixed, so tiles still agree on their edges; cropland biome
    /// cells under a mapped non-farm land use are not farmed, <see cref="CropLand"/>), and skirts
    /// hanging down on all four edges. Positions are relative to the area's
    /// south-west corner with absolute heights; adjacent areas drawn with the same step from tiles whose shared
    /// edges match (the GHT1 no-cracks invariant) produce identical edge vertices.
    /// <para>Vertex order: the (Quads + 1)^2 grid vertices row by row from the south-west corner, then the skirt
    /// vertices. Appends to <see cref="MeshData"/>; call <see cref="MeshData.Clear"/> first for a fresh mesh.
    /// Allocation-free apart from buffer growth and the per-tile <see cref="CropLand"/> raster (built once per tile);
    /// thread-safe for distinct <see cref="MeshData"/> instances.</para>
    /// </summary>
    public static class TerrainMesher
    {
        /// <summary>Mesh a whole tile (the M1_PLAN form): <c>Build(t, t.Tile, o, m)</c>.</summary>
        public static void Build(TileData t, TerrainOptions o, MeshData m)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            Build(t, t.Tile, o, m);
        }

        /// <summary>Mesh <paramref name="area"/> from <paramref name="source"/> (the area itself or an ancestor).
        /// Emits nothing when the source has no height grid.</summary>
        public static void Build(TileData source, TileId area, TerrainOptions o, MeshData m)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new TerrainOptions();
            if (!TerrainGrid.HasHeights(source)) return;
            TerrainGrid g = TerrainGrid.For(source, area, o.Step, o.Neighbours);

            int q = g.Quads, side = q + 1;
            float skirt = o.SkirtDepthM < 0f ? TerrainOptions.AutoSkirtDepthM(g.CellM) : o.SkirtDepthM;
            int skirtVerts = skirt > 0f ? 4 * side : 0;
            m.Reserve(side * side + skirtVerts, q * q * 6 + (skirt > 0f ? 4 * q * 6 : 0));
            int baseV = m.VertexCount;

            int n = source.HeightsN;
            int bn = source.Biomes != null && source.BiomesN >= 2 && source.Biomes.Length >= source.BiomesN * source.BiomesN
                ? source.BiomesN : 0;

            // Farmed land (CropLand): a cropland biome cell under a mapped non-farm land use (a parade ground, a park, a
            // residential block) is coloured as urban green, not as paddy.
            CropLand crop = o.Detail && bn > 0 && source.Areas.Count > 0 ? CropLand.For(source) : null;
            // Micro-relief: hummocks of about 30 m, full strength on 8-16 m grids, gone by 48 m (it would alias there).
            float relief = o.MicroRelief * 0.9f * (float)Math.Max(0.0, Math.Min(1.0, (48.0 - g.CellM) / 32.0));
            TerrainCarve carve = o.Carve && !g.SubSample ? TileHeightSampler.ForArea(source, area, o.Step, o.Neighbours).Carve : null;
            for (int k = 0; k <= q; k++)
            {
                float pz = (float)(k * g.CellM);
                for (int l = 0; l <= q; l++)
                {
                    float px = (float)(l * g.CellM);
                    float h = carve != null ? carve.CoarseVertex(k, l) : g.VertexHeight(source, k, l);
                    double gx, gz;
                    if (carve != null && MovedNear(carve, k, l, q)) CoarseGradient(carve, k, l, q, g.CellM, out gx, out gz);
                    else g.VertexGradient(source, k, l, out gx, out gz);
                    Biome biome;
                    if (bn == 0) biome = Biome.None;
                    else if (!g.SubSample)
                        biome = source.Biomes[NearestBiomeIndex(g.SourceRow(k), n, bn) * bn + NearestBiomeIndex(g.SourceColumn(l), n, bn)];
                    else biome = BiomeAt(source, bn, area.X0 + px, area.Z0 + pz);
                    if (crop != null && FieldPattern.IsCrop(biome) && crop.Excluded(area.X0 + px - source.Tile.X0, area.Z0 + pz - source.Tile.Z0))
                        biome = Biome.UrbanGreen;
                    float nx, ny, nz;
                    TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
                    uint c = o.SlopeColours ? BiomePalette.Ground(biome, o.Season, ny) : BiomePalette.Rgba(biome, o.Season);
                    // A coarse vertex carved beside a hill road shows the cut or fill (TerrainCarve.PreCarve).
                    bool carved = carve != null && carve.IsCarvedVertex(k, l);
                    float delta = carved ? h - carve.NaturalVertex(k, l) : 0f;
                    double wx = area.X0 + px, wz = area.Z0 + pz;
                    if (!o.Detail)
                    {
                        if (carved)
                        {
                            MaterialChannel unused;
                            float uw;
                            c = CutFill.Blend(c, MaterialChannel.Plain, delta, ny, h, wx, wz, out unused, out uw);
                        }
                        m.AddVertex(px, h, pz, nx, ny, nz, c);
                        continue;
                    }
                    MaterialChannel ch;
                    float ao;
                    c = TerrainLook.Colour(biome, o.Season, c, ny, h, wx, wz, out ch, out ao);
                    if (carved)
                    {
                        MaterialChannel cch;
                        float w;
                        c = CutFill.Blend(c, ch, delta, ny, h, wx, wz, out cch, out w);
                        if (w > 0.5f) ch = cch;
                    }
                    if (relief > 0f)
                    {
                        float mx, mz;
                        TerrainNoise.FbmGradient(wx, wz, 34.0, 2, 0x4D52u, out mx, out mz);
                        TileHeightSampler.FacetNormal(gx + mx * relief, gz + mz * relief, out nx, out ny, out nz);
                    }
                    m.AddVertex(px, h, pz, nx, ny, nz, c, (float)ch, ao);
                }
            }

            if (carve == null)
            {
                for (int k = 0; k < q; k++)
                {
                    int row = baseV + k * side;
                    for (int l = 0; l < q; l++)
                    {
                        int v00 = row + l, v10 = v00 + 1, v01 = v00 + side, v11 = v01 + 1;
                        m.AddTriangle(v00, v01, v11);
                        m.AddTriangle(v00, v11, v10);
                    }
                }
                if (skirt > 0f)
                {
                    // Each edge walked with the outside on its right: south W->E, east S->N, north E->W, west N->S.
                    Skirt(m, baseV, side, skirt, 0, 0, 0, 1);
                    Skirt(m, baseV, side, skirt, 0, q, 1, 0);
                    Skirt(m, baseV, side, skirt, q, q, 0, -1);
                    Skirt(m, baseV, side, skirt, q, 0, -1, 0);
                }
                return;
            }
            CarvedCells(source, area, g, carve, o, crop, bn, n, relief, baseV, skirt, m);
        }

        /// <summary>True when coarse vertex (k, l) or one of its four neighbours was moved by the carve.</summary>
        private static bool MovedNear(TerrainCarve carve, int k, int l, int q)
        {
            if (carve.IsMoved(k, l)) return true;
            if (k > 0 && carve.IsMoved(k - 1, l) || k < q && carve.IsMoved(k + 1, l)) return true;
            return l > 0 && carve.IsMoved(k, l - 1) || l < q && carve.IsMoved(k, l + 1);
        }

        /// <summary>Central differences of the coarse heights as drawn (one-sided on the border).</summary>
        private static void CoarseGradient(TerrainCarve carve, int k, int l, int q, double cell, out double gx, out double gz)
        {
            int l0 = Math.Max(0, l - 1), l1 = Math.Min(q, l + 1), k0 = Math.Max(0, k - 1), k1 = Math.Min(q, k + 1);
            gx = (carve.CoarseVertex(k, l1) - carve.CoarseVertex(k, l0)) / ((l1 - l0) * cell);
            gz = (carve.CoarseVertex(k1, l) - carve.CoarseVertex(k0, l)) / ((k1 - k0) * cell);
        }

        private sealed class CarveScratch
        {
            public int[] Index = new int[0];
            public int[] Stamp = new int[0];
            public int Mark;
            public readonly List<int> Loop = new List<int>(64);

            public void Ensure(int n)
            {
                if (Index.Length >= n) return;
                Index = new int[n];
                Stamp = new int[n];
                Mark = 0;
            }
        }

        [ThreadStatic] private static CarveScratch _carveScratch;

        /// <summary>
        /// The cells of a carved area (<see cref="TerrainCarve"/>): refined cells as <c>Sub × Sub</c> sub-quads on the carved
        /// heights (each split along its (a, b)-(a+1, b+1) diagonal, the rule <see cref="TileHeightSampler"/> interpolates
        /// with), unrefined cells beside a refined one as a fan from their centre through the refined neighbour's edge
        /// vertices (exactly the natural plane of their two triangles, so no T-junction and no crack), the rest as the
        /// natural two triangles; then skirts that follow the refined edges.
        /// </summary>
        private static void CarvedCells(TileData source, TileId area, TerrainGrid g, TerrainCarve carve, TerrainOptions o, CropLand crop, int bn, int n,
                                        float relief, int baseV, float skirt, MeshData m)
        {
            int q = g.Quads, side = q + 1, r = carve.Sub, gs = q * r + 1;
            CarveScratch sc = _carveScratch ?? (_carveScratch = new CarveScratch());
            sc.Ensure(gs * gs);
            if (++sc.Mark == int.MaxValue)
            {
                Array.Clear(sc.Stamp, 0, sc.Stamp.Length);
                sc.Mark = 1;
            }
            var ctx = new SubContext
            {
                Source = source, Area = area, G = g, Carve = carve, O = o, Crop = crop, Bn = bn, N = n, Relief = relief, BaseV = baseV, M = m, Sc = sc,
                Gs = gs, R = r, Side = side,
            };
            for (int j = 0; j < q; j++)
            for (int i = 0; i < q; i++)
            {
                if (carve.IsRefined(i, j))
                {
                    for (int b = 0; b < r; b++)
                    for (int a = 0; a < r; a++)
                    {
                        int gi = i * r + a, gj = j * r + b;
                        int v00 = ctx.V(gi, gj), v10 = ctx.V(gi + 1, gj), v01 = ctx.V(gi, gj + 1), v11 = ctx.V(gi + 1, gj + 1);
                        m.AddTriangle(v00, v01, v11);
                        m.AddTriangle(v00, v11, v10);
                    }
                    continue;
                }
                bool subW = carve.IsRefined(i - 1, j), subN = carve.IsRefined(i, j + 1), subE = carve.IsRefined(i + 1, j), subS = carve.IsRefined(i, j - 1);
                if (!subW && !subN && !subE && !subS)
                {
                    int v00 = baseV + j * side + i, v10 = v00 + 1, v01 = v00 + side, v11 = v01 + 1;
                    m.AddTriangle(v00, v01, v11);
                    m.AddTriangle(v00, v11, v10);
                    continue;
                }
                // Fan from the cell centre (on the diagonal, so every fan triangle lies in one of the two natural triangles).
                List<int> loop = sc.Loop;
                loop.Clear();
                int i0 = i * r, j0 = j * r, i1 = i0 + r, j1 = j0 + r;
                for (int t = 0; t < (subW ? r : 1); t++) loop.Add(ctx.V(i0, j0 + t));
                for (int t = 0; t < (subN ? r : 1); t++) loop.Add(ctx.V(i0 + t, j1));
                for (int t = 0; t < (subE ? r : 1); t++) loop.Add(ctx.V(i1, j1 - t));
                for (int t = 0; t < (subS ? r : 1); t++) loop.Add(ctx.V(i1 - t, j0));
                int c00 = baseV + j * side + i, c11 = c00 + side + 1;
                int centre = m.VertexCount;
                AverageVertex(m, c00, c11);
                for (int k = 0; k < loop.Count; k++) m.AddTriangle(centre, loop[k], loop[(k + 1) % loop.Count]);
            }
            if (o.Walls) carve.AppendWalls(m);
            if (skirt <= 0f) return;
            List<int> top = sc.Loop;
            // South edge W->E, east S->N, north E->W, west N->S (the outside on the right).
            top.Clear();
            for (int l = 0; l < q; l++)
                for (int t = 0; t < (carve.IsRefined(l, 0) ? r : 1); t++) top.Add(ctx.V(l * r + t * (carve.IsRefined(l, 0) ? 1 : r), 0));
            top.Add(ctx.V(q * r, 0));
            SkirtAlong(m, top, skirt);
            top.Clear();
            for (int k = 0; k < q; k++)
                for (int t = 0; t < (carve.IsRefined(q - 1, k) ? r : 1); t++) top.Add(ctx.V(q * r, k * r + t * (carve.IsRefined(q - 1, k) ? 1 : r)));
            top.Add(ctx.V(q * r, q * r));
            SkirtAlong(m, top, skirt);
            top.Clear();
            for (int l = q - 1; l >= 0; l--)
                for (int t = 0; t < (carve.IsRefined(l, q - 1) ? r : 1); t++) top.Add(ctx.V((l + 1) * r - t * (carve.IsRefined(l, q - 1) ? 1 : r), q * r));
            top.Add(ctx.V(0, q * r));
            SkirtAlong(m, top, skirt);
            top.Clear();
            for (int k = q - 1; k >= 0; k--)
                for (int t = 0; t < (carve.IsRefined(0, k) ? r : 1); t++) top.Add(ctx.V(0, (k + 1) * r - t * (carve.IsRefined(0, k) ? 1 : r)));
            top.Add(ctx.V(0, 0));
            SkirtAlong(m, top, skirt);
        }

        /// <summary>A vertex halfway between vertices a and b (position, normal, colour, UV0).</summary>
        private static void AverageVertex(MeshData m, int a, int b)
        {
            int pa = 3 * a, pb = 3 * b, ca = 4 * a, cb = 4 * b;
            float x = 0.5f * (m.Positions[pa] + m.Positions[pb]), y = 0.5f * (m.Positions[pa + 1] + m.Positions[pb + 1]), z = 0.5f * (m.Positions[pa + 2] + m.Positions[pb + 2]);
            float nx = m.Normals[pa] + m.Normals[pb], ny = m.Normals[pa + 1] + m.Normals[pb + 1], nz = m.Normals[pa + 2] + m.Normals[pb + 2];
            float inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            uint rgba = (uint)((m.Colors[ca] + m.Colors[cb]) / 2 << 24 | (m.Colors[ca + 1] + m.Colors[cb + 1]) / 2 << 16 |
                               (m.Colors[ca + 2] + m.Colors[cb + 2]) / 2 << 8 | (m.Colors[ca + 3] + m.Colors[cb + 3]) / 2);
            if (m.HasUv0)
                m.AddVertex(x, y, z, nx * inv, ny * inv, nz * inv, rgba, m.Uv0[2 * a], 0.5f * (m.Uv0[2 * a + 1] + m.Uv0[2 * b + 1]));
            else m.AddVertex(x, y, z, nx * inv, ny * inv, nz * inv, rgba);
        }

        /// <summary>Hang a skirt below a polyline of top vertices (walked with the outside on its right).</summary>
        private static void SkirtAlong(MeshData m, List<int> top, float depth)
        {
            int first = m.VertexCount;
            for (int t = 0; t < top.Count; t++)
            {
                int v = top[t], p = v * 3, c = v * 4;
                uint rgba = (uint)(m.Colors[c] << 24 | m.Colors[c + 1] << 16 | m.Colors[c + 2] << 8 | m.Colors[c + 3]);
                if (m.HasUv0)
                    m.AddVertex(m.Positions[p], m.Positions[p + 1] - depth, m.Positions[p + 2], m.Normals[p], m.Normals[p + 1], m.Normals[p + 2], rgba,
                                m.Uv0[2 * v], m.Uv0[2 * v + 1] * 0.6f);
                else m.AddVertex(m.Positions[p], m.Positions[p + 1] - depth, m.Positions[p + 2], m.Normals[p], m.Normals[p + 1], m.Normals[p + 2], rgba);
            }
            for (int t = 0; t + 1 < top.Count; t++)
            {
                int topA = top[t], topB = top[t + 1], botA = first + t, botB = first + t + 1;
                m.AddTriangle(botA, topA, topB);
                m.AddTriangle(botA, topB, botB);
            }
        }

        /// <summary>The vertices of a carved area's global sub-grid, created on demand (a point on the coarse grid at its
        /// natural height reuses the coarse vertex).</summary>
        private struct SubContext
        {
            public TileData Source;
            public TileId Area;
            public TerrainGrid G;
            public TerrainCarve Carve;
            public TerrainOptions O;
            public CropLand Crop;
            public int Bn, N, BaseV, Gs, R, Side;
            public float Relief;
            public MeshData M;
            public CarveScratch Sc;

            /// <summary>The drawn height at global sub-grid point (gi, gj): a refined cell's stored height, else the natural
            /// surface.</summary>
            public float Height(int gi, int gj, out bool carved)
            {
                int r = R, q = G.Quads;
                int ia = gi % r == 0 ? gi / r - 1 : gi / r, ib = gi / r, ja = gj % r == 0 ? gj / r - 1 : gj / r, jb = gj / r;
                for (int cj = ja; cj <= jb; cj++)
                for (int ci = ia; ci <= ib; ci++)
                {
                    if (ci < 0 || cj < 0 || ci >= q || cj >= q || !Carve.IsRefined(ci, cj)) continue;
                    carved = true;
                    return Carve.SubVertex(ci, cj, gi - ci * r, gj - cj * r);
                }
                carved = false;
                double du, dv;
                return (float)Carve.CoarseSurface(gi / (double)r, gj / (double)r, out du, out dv);
            }

            public int V(int gi, int gj)
            {
                int idx = gj * Gs + gi;
                if (Sc.Stamp[idx] == Sc.Mark) return Sc.Index[idx];
                int r = R;
                bool carved;
                float h = Height(gi, gj, out carved);
                double du, dv;
                float nat = (float)Carve.CoarseSurface(gi / (double)r, gj / (double)r, out du, out dv);
                int v;
                if (gi % r == 0 && gj % r == 0 && Math.Abs(h - nat) < 1e-4f) v = BaseV + gj / r * Side + gi / r;
                else v = Emit(gi, gj, h, Carve.NaturalSurface(gi / (double)r, gj / (double)r));
                Sc.Index[idx] = v;
                Sc.Stamp[idx] = Sc.Mark;
                return v;
            }

            private int Emit(int gi, int gj, float h, float nat)
            {
                int r = R, q = G.Quads, last = q * r;
                double subM = Carve.SubM;
                float px = (float)(gi * subM), pz = (float)(gj * subM);
                double wx = Area.X0 + px, wz = Area.Z0 + pz;
                float delta = h - nat;
                bool moved = Math.Abs(delta) > 1e-4f;
                // Natural shading normal (as the coarse vertices around) and, where carved, the carved surface's own.
                float nnx, nny, nnz;
                G.SmoothNormal(Source, gi / (double)r, gj / (double)r, out nnx, out nny, out nnz);
                double gx = -nnx / nny, gz = -nnz / nny;
                if (moved)
                {
                    bool c;
                    float hl = gi > 0 ? Height(gi - 1, gj, out c) : h, hr = gi < last ? Height(gi + 1, gj, out c) : h;
                    float hd = gj > 0 ? Height(gi, gj - 1, out c) : h, hu = gj < last ? Height(gi, gj + 1, out c) : h;
                    int wxn = (gi > 0 ? 1 : 0) + (gi < last ? 1 : 0), wzn = (gj > 0 ? 1 : 0) + (gj < last ? 1 : 0);
                    gx = wxn > 0 ? (hr - hl) / (wxn * subM) : 0.0;
                    gz = wzn > 0 ? (hu - hd) / (wzn * subM) : 0.0;
                }
                float nx, ny, nz;
                TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
                Biome biome = Bn == 0 ? Biome.None : BiomeAt(Source, Bn, wx, wz);
                if (Crop != null && FieldPattern.IsCrop(biome) && Crop.Excluded(wx - Source.Tile.X0, wz - Source.Tile.Z0)) biome = Biome.UrbanGreen;
                uint c0 = O.SlopeColours ? BiomePalette.Ground(biome, O.Season, ny) : BiomePalette.Rgba(biome, O.Season);
                if (!O.Detail)
                {
                    if (moved)
                    {
                        MaterialChannel unused;
                        float uw;
                        c0 = CutFill.Blend(c0, MaterialChannel.Plain, delta, ny, h, wx, wz, out unused, out uw);
                    }
                    return M.AddVertex(px, h, pz, nx, ny, nz, c0);
                }
                MaterialChannel ch;
                float ao;
                uint col = TerrainLook.Colour(biome, O.Season, c0, ny, h, wx, wz, out ch, out ao);
                if (moved)
                {
                    MaterialChannel cch;
                    float w;
                    col = CutFill.Blend(col, ch, delta, ny, h, wx, wz, out cch, out w);
                    if (w > 0.5f) ch = cch;
                    ao *= 1f - 0.12f * w * (1f - ny);
                }
                if (Relief > 0f)
                {
                    float mx, mz;
                    TerrainNoise.FbmGradient(wx, wz, 34.0, 2, 0x4D52u, out mx, out mz);
                    float k = moved ? 0.5f * Relief : Relief;
                    TileHeightSampler.FacetNormal(gx + mx * k, gz + mz * k, out nx, out ny, out nz);
                }
                return M.AddVertex(px, h, pz, nx, ny, nz, col, (float)ch, ao);
            }
        }

        /// <summary>Hang a skirt below the grid edge starting at vertex (k0, l0) and stepping (dk, dl).</summary>
        private static void Skirt(MeshData m, int baseV, int side, float depth, int k0, int l0, int dk, int dl)
        {
            int first = m.VertexCount;
            for (int t = 0; t < side; t++)
            {
                int top = baseV + (k0 + t * dk) * side + l0 + t * dl;
                int p = top * 3, c = top * 4;
                uint rgba = (uint)(m.Colors[c] << 24 | m.Colors[c + 1] << 16 | m.Colors[c + 2] << 8 | m.Colors[c + 3]);
                if (m.HasUv0)
                    m.AddVertex(m.Positions[p], m.Positions[p + 1] - depth, m.Positions[p + 2], m.Normals[p], m.Normals[p + 1], m.Normals[p + 2], rgba,
                                m.Uv0[2 * top], m.Uv0[2 * top + 1] * 0.6f);
                else
                    m.AddVertex(m.Positions[p], m.Positions[p + 1] - depth, m.Positions[p + 2],
                                m.Normals[p], m.Normals[p + 1], m.Normals[p + 2], rgba);
            }
            for (int t = 0; t < side - 1; t++)
            {
                int topA = baseV + (k0 + t * dk) * side + l0 + t * dl;
                int topB = baseV + (k0 + (t + 1) * dk) * side + l0 + (t + 1) * dl;
                int botA = first + t, botB = first + t + 1;
                m.AddTriangle(botA, topA, topB);
                m.AddTriangle(botA, topB, botB);
            }
        }

        /// <summary>Nearest index on a vertex-aligned grid of <paramref name="bn"/> samples for sample
        /// <paramref name="i"/> of an <paramref name="n"/>-sample grid (halves round up).</summary>
        internal static int NearestBiomeIndex(int i, int n, int bn)
        {
            return (int)(((long)i * (bn - 1) * 2 + (n - 1)) / (2L * (n - 1)));
        }

        /// <summary>Nearest BIOM sample at game (x, z), clamped onto the tile.</summary>
        internal static Biome BiomeAt(TileData t, int bn, double x, double z)
        {
            double s = t.Tile.Size / (bn - 1);
            int i = (int)Math.Floor((x - t.Tile.X0) / s + 0.5), j = (int)Math.Floor((z - t.Tile.Z0) / s + 0.5);
            if (i < 0) i = 0;
            else if (i > bn - 1) i = bn - 1;
            if (j < 0) j = 0;
            else if (j > bn - 1) j = bn - 1;
            return t.Biomes[j * bn + i];
        }
    }

    /// <summary>
    /// The per-vertex detail look of the terrain (TerrainMesher with <see cref="TerrainOptions.Detail"/>), a function of
    /// the biome, season, slope, height and world position only (so tiles and LODs agree): forest ground mottled into
    /// a dark canopy shell as seen from the hills, grass and scrub with dry and lush patches, cropland as a patchwork
    /// of plots (<see cref="FieldPattern"/>), rock outcrops breaking through steep ground in irregular patches, and the
    /// material channel (Foliage on forest, Grass on green ground and crops, Dirt on bare soil and ploughed plots, Stone
    /// on rock and gravel) with an AO that darkens steep and low-lying ground.
    /// </summary>
    public static class TerrainLook
    {
        /// <summary>
        /// The detail colour (0xRRGGBBAA) of a terrain vertex of biome <paramref name="b"/> with base colour
        /// <paramref name="baseRgba"/>, normal Y <paramref name="ny"/> and world position (<paramref name="wx"/>,
        /// <paramref name="wz"/>), with its material channel and AO.
        /// </summary>
        public static uint Colour(Biome b, Season season, uint baseRgba, float ny, float height, double wx, double wz, out MaterialChannel ch, out float ao)
        {
            float patch = TerrainNoise.Fbm(wx, wz, 90.0, 2, 0x5041u), fine = TerrainNoise.Fbm(wx, wz, 23.0, 2, 0x4649u);
            float slope = ny >= 1f ? 0f : (float)(Math.Acos(Math.Max(0f, Math.Min(1f, ny))) * (180.0 / Math.PI));
            uint c = baseRgba;
            ch = MaterialChannel.Grass;
            int family = Data.AreaTypeGrid.BiomeFamily(b);
            float start, end;
            BiomePalette.SlopeBlendDeg(b, out start, out end);
            // Rock outcrops: the rock colour breaks through in noisy patches before the slope band is reached.
            float rockT = Smooth(end - 6f, end + 12f, slope + 9f * fine);
            // Crops cover the flats and the terraced slopes (terraces are cut into hillsides up to ~38°).
            if (FieldPattern.IsCrop(b) && slope < Math.Max(start, 38f))
            {
                bool bare;
                uint crop = FieldPattern.CropColour(FieldPattern.PlotHash(wx, wz), season, out bare);
                c = MeshColor.Lerp(c, MeshColor.FromHex(crop), 0.78f);
                ch = bare ? MaterialChannel.Dirt : MaterialChannel.Grass;
            }
            else if (family == 1)
            {
                // Forest: a dark mottled canopy shell (seen from the rim and the hills), lighter crowns in the sun.
                uint dark = MeshColor.Scale(c, 0.72f), light = MeshColor.Lerp(c, MeshColor.FromHex(0x7FB04A), 0.35f);
                c = MeshColor.Lerp(dark, light, 0.5f + 0.5f * fine);
                ch = MaterialChannel.Foliage;
            }
            else if (b == Biome.RiverbedGravel || b == Biome.ScreeRock || b == Biome.Moraine)
            {
                ch = MaterialChannel.Stone;
            }
            else if (b == Biome.BareSoil || b == Biome.UrbanDense || b == Biome.Water)
            {
                ch = MaterialChannel.Dirt;
            }
            else if (b == Biome.Snow || b == Biome.Glacier)
            {
                ch = MaterialChannel.Plain;
            }
            else
            {
                // Grass and scrub: dry straw patches and lush hollows.
                float dry = Smooth(0.15f, 0.65f, patch);
                c = MeshColor.Lerp(c, MeshColor.FromHex(season == Season.Monsoon ? 0x9CB850u : 0xB8A86Au), 0.35f * dry);
                c = MeshColor.Lerp(c, MeshColor.FromHex(0x5E9A3A), 0.25f * Smooth(0.2f, 0.7f, -patch));
            }
            if (slope > start && ch == MaterialChannel.Grass) ch = slope > end ? MaterialChannel.Dirt : ch;
            if (rockT > 0.01f && b != Biome.Snow && b != Biome.Glacier)
            {
                c = MeshColor.Lerp(c, BiomePalette.RockRgba(b), rockT);
                if (rockT > 0.5f) ch = MaterialChannel.Stone;
            }
            c = MeshColor.Scale(c, 1f + 0.07f * patch + 0.04f * fine);
            ao = 1f - 0.28f * Smooth(10f, 50f, slope) - 0.08f * Math.Max(0f, -fine);
            return c | 0xFFu;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = (x - e0) / (e1 - e0);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
