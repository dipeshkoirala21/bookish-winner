using System;
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
    /// <see cref="BiomePalette"/>, and skirts hanging down on all four edges. Positions are relative to the area's
    /// south-west corner with absolute heights; adjacent areas drawn with the same step from tiles whose shared
    /// edges match (the GHT1 no-cracks invariant) produce identical edge vertices.
    /// <para>Vertex order: the (Quads + 1)^2 grid vertices row by row from the south-west corner, then the skirt
    /// vertices. Appends to <see cref="MeshData"/>; call <see cref="MeshData.Clear"/> first for a fresh mesh.
    /// Allocation-free apart from buffer growth; thread-safe for distinct <see cref="MeshData"/> instances.</para>
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

            for (int k = 0; k <= q; k++)
            {
                float pz = (float)(k * g.CellM);
                for (int l = 0; l <= q; l++)
                {
                    float px = (float)(l * g.CellM);
                    float h = g.VertexHeight(source, k, l);
                    double gx, gz;
                    g.VertexGradient(source, k, l, out gx, out gz);
                    Biome biome;
                    if (bn == 0) biome = Biome.None;
                    else if (!g.SubSample)
                        biome = source.Biomes[NearestBiomeIndex(g.SourceRow(k), n, bn) * bn + NearestBiomeIndex(g.SourceColumn(l), n, bn)];
                    else biome = BiomeAt(source, bn, area.X0 + px, area.Z0 + pz);
                    float nx, ny, nz;
                    TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
                    uint c = o.SlopeColours ? BiomePalette.Ground(biome, o.Season, ny) : BiomePalette.Rgba(biome, o.Season);
                    m.AddVertex(px, h, pz, nx, ny, nz, c);
                }
            }

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
}
