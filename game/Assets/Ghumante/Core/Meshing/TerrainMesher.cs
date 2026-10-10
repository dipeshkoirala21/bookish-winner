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

        /// <summary>
        /// The detail look (docs/W2_DETAIL_CONTRACT.md §1.6, §5): world-fixed colour patches (dry grass, canopy mottling
        /// on forest, rock outcrops breaking through steep ground), the field patchwork of cropland (paddy, mustard,
        /// wheat, stubble by season), micro-relief in the normals (hummocky ground; fades out on coarse grids) and UV0
        /// material channels with baked AO. Off: the plain W1 biome palette with exact central-difference normals.
        /// </summary>
        public bool Detail = true;

        /// <summary>Strength of the micro-relief in the normals (0 none; 1 the default hummocks).</summary>
        public float MicroRelief = 1f;

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
                    if (crop != null && FieldPattern.IsCrop(biome) && crop.Excluded(area.X0 + px - source.Tile.X0, area.Z0 + pz - source.Tile.Z0))
                        biome = Biome.UrbanGreen;
                    float nx, ny, nz;
                    TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
                    uint c = o.SlopeColours ? BiomePalette.Ground(biome, o.Season, ny) : BiomePalette.Rgba(biome, o.Season);
                    if (!o.Detail)
                    {
                        m.AddVertex(px, h, pz, nx, ny, nz, c);
                        continue;
                    }
                    double wx = area.X0 + px, wz = area.Z0 + pz;
                    MaterialChannel ch;
                    float ao;
                    c = TerrainLook.Colour(biome, o.Season, c, ny, h, wx, wz, out ch, out ao);
                    if (relief > 0f)
                    {
                        float mx, mz;
                        TerrainNoise.FbmGradient(wx, wz, 34.0, 2, 0x4D52u, out mx, out mz);
                        TileHeightSampler.FacetNormal(gx + mx * relief, gz + mz * relief, out nx, out ny, out nz);
                    }
                    m.AddVertex(px, h, pz, nx, ny, nz, c, (float)ch, ao);
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
