using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>Options for <see cref="AreaMesher"/>.</summary>
    public sealed class AreaOptions
    {
        /// <summary>Lift above the terrain for water (below roads at 0.25 m, above green areas).</summary>
        public float WaterLiftM = 0.14f;

        /// <summary>Lift above the terrain for parks and other green kinds.</summary>
        public float GreenLiftM = 0.10f;

        /// <summary>Lift for the subtle land-use tints (only with <see cref="IncludeSubtle"/>).</summary>
        public float SubtleLiftM = 0.06f;

        /// <summary>Extra lift per kind rank inside a family (<see cref="AreaStyle.KindRank"/>, 0..9: the more
        /// specific kind higher, a pitch over a park over a forest), so overlapping records of one family are not
        /// coplanar.</summary>
        public float KindLiftStepM = 0.004f;

        /// <summary>Extra lift per size rank inside a kind (<see cref="AreaMesher.SizeRank"/>, 0..3: smaller records
        /// higher). The defaults keep the whole in-family band (9 × 4 mm + 3 × 1 mm) below the 40 mm gap between
        /// families.</summary>
        public float SizeLiftStepM = 0.001f;

        /// <summary>Also draw built-up and bare land uses as subtle tints (residential, commercial, sand, ...).</summary>
        public bool IncludeSubtle = false;

        /// <summary>Longest edge when the sampler is not a <see cref="TileHeightSampler"/> (triangles are then
        /// subdivided uniformly instead of clipped to the terrain grid); 0 means 8 m.</summary>
        public float MaxEdgeM = 0f;

        /// <summary>Cap on the per-triangle subdivision of that fallback.</summary>
        public int MaxSubdivision = 64;

        /// <summary>
        /// The detail look (docs/W2_DETAIL_CONTRACT.md §1.6, §5): UV0 material channels and AO on every surface (Water,
        /// Grass, Foliage, Dirt, Stone, Flagstone, Concrete), world-fixed colour patches, a shallow light rim inside
        /// water bodies, farmland in the season's crop patchwork. Off: flat W1 colours without UV0.
        /// </summary>
        public bool Detail = true;

        /// <summary>Palette season (crop colours, dry grass); set it to the terrain's.</summary>
        public Season Season = BiomePalette.DefaultSeason;

        /// <summary>Banks: a 2.5 m strip of gravel, sand or mud outside every water area (needs a
        /// <see cref="TileHeightSampler"/>).</summary>
        public bool WaterBanks = true;

        public float BankWidthM = 2.5f, BankLiftM = 0.075f;

        /// <summary>
        /// Field lines on farmed land (<see cref="CropLand"/>: farmland areas and cropland biome cells no other land use
        /// covers; needs a <see cref="TileHeightSampler"/>): on terraced slopes thin riser lines (and, with
        /// <see cref="TerraceLips"/>, their bright lips) along world-fixed contours every
        /// <see cref="FieldPattern.TerraceStepM"/>, on the flat the grassy bunds between plots. Exactly on the drawn
        /// terrain triangles at <see cref="FieldLiftM"/>, at most <see cref="MaxFieldLineTris"/> triangles per call
        /// (scaled down with the terrain step).
        /// </summary>
        public bool FieldLines = true;

        /// <summary>Lift of the field lines above the terrain (above the green areas, below water and roads).</summary>
        public float FieldLiftM = 0.13f;

        /// <summary>Triangle budget of the field lines per tile at terrain step 1 (a coarser step divides it by the
        /// step: the terrain itself has a quarter of the triangles at step 2). Over it the bunds thin first, then every
        /// second terrace level is drawn, then whole world-fixed blocks drop evenly over the tile. The default is the
        /// Mid tier's (<see cref="FieldLineCap"/>); tile builds set the tier's.</summary>
        public int MaxFieldLineTris = FieldLineCap(1);

        /// <summary>The field-line budget per tile at terrain step 1 for a device tier (0 Low, 1 Mid, 2 High): 3 000 /
        /// 8 000 / 16 000 triangles (W2_DESIGN 10.4: terrain and areas share the terrain slice of 30 k / 90 k / 160 k).</summary>
        public static int FieldLineCap(int tier)
        {
            return tier <= 0 ? 3000 : tier == 1 ? 8000 : 16000;
        }

        /// <summary>The field-line budget of a tier for a tile drawn at terrain <paramref name="step"/> (what
        /// <see cref="AreaMesher"/> applies when <see cref="MaxFieldLineTris"/> is <see cref="FieldLineCap(int)"/>).</summary>
        public static int FieldLineCap(int tier, int step)
        {
            return FieldLineCap(tier) / Math.Max(1, step);
        }

        /// <summary>The area options of a device tier (the field-line budget; everything else the defaults).</summary>
        public static AreaOptions ForTier(int tier)
        {
            return new AreaOptions { MaxFieldLineTris = FieldLineCap(tier) };
        }

        /// <summary>Also draw the light lip band along each terrace edge (doubles the terrace triangles).</summary>
        public bool TerraceLips = false;
    }

    /// <summary>How an <see cref="AreaKind"/> is drawn.</summary>
    public enum AreaFamily : byte
    {
        None = 0,
        Water = 1,
        Green = 2,
        Subtle = 3,
    }

    /// <summary>Area kinds to families and colours (ASSET_MANIFEST.md 1.9 tokens and section 2 greens).</summary>
    public static class AreaStyle
    {
        public static AreaFamily Family(AreaKind k)
        {
            switch (k)
            {
                case AreaKind.WaterLake:
                case AreaKind.WaterRiver:
                case AreaKind.WaterPond:
                case AreaKind.Wetland: return AreaFamily.Water;
                case AreaKind.Forest:
                case AreaKind.Farmland:
                case AreaKind.Orchard:
                case AreaKind.Meadow:
                case AreaKind.Scrub:
                case AreaKind.Park:
                case AreaKind.Pitch:
                case AreaKind.Grassland:
                case AreaKind.Cemetery:
                case AreaKind.TeaGarden: return AreaFamily.Green;
                case AreaKind.Residential:
                case AreaKind.Commercial:
                case AreaKind.Industrial:
                case AreaKind.Religious:
                case AreaKind.Pedestrian:
                case AreaKind.SandShingle:
                case AreaKind.BareRock:
                case AreaKind.Aerodrome:
                case AreaKind.Scree:
                case AreaKind.Glacier: return AreaFamily.Subtle;
                default: return AreaFamily.None; // None, Protected, Military: boundaries, not surfaces
            }
        }

        /// <summary>
        /// Draw order of a kind inside its family (higher draws on top where records overlap): the smaller, more
        /// specific kinds above the broad ones. Green: forest, scrub, grassland, meadow, farmland, orchard, tea
        /// garden, cemetery, park, pitch. Water: wetland, river, lake, pond. Subtle: glacier, scree, bare rock,
        /// sand, residential, industrial, commercial, aerodrome, religious, pedestrian. 0 for undrawn kinds.
        /// </summary>
        public static int KindRank(AreaKind k)
        {
            switch (k)
            {
                case AreaKind.Forest: return 0;
                case AreaKind.Scrub: return 1;
                case AreaKind.Grassland: return 2;
                case AreaKind.Meadow: return 3;
                case AreaKind.Farmland: return 4;
                case AreaKind.Orchard: return 5;
                case AreaKind.TeaGarden: return 6;
                case AreaKind.Cemetery: return 7;
                case AreaKind.Park: return 8;
                case AreaKind.Pitch: return 9;
                case AreaKind.Wetland: return 0;
                case AreaKind.WaterRiver: return 1;
                case AreaKind.WaterLake: return 2;
                case AreaKind.WaterPond: return 3;
                case AreaKind.Glacier: return 0;
                case AreaKind.Scree: return 1;
                case AreaKind.BareRock: return 2;
                case AreaKind.SandShingle: return 3;
                case AreaKind.Residential: return 4;
                case AreaKind.Industrial: return 5;
                case AreaKind.Commercial: return 6;
                case AreaKind.Aerodrome: return 7;
                case AreaKind.Religious: return 8;
                case AreaKind.Pedestrian: return 9;
                default: return 0;
            }
        }

        public static uint Rgba(AreaKind k)
        {
            switch (k)
            {
                case AreaKind.WaterLake:
                case AreaKind.WaterPond: return MeshColor.FromHex(0x3FA9D6); // w.water.lake
                case AreaKind.WaterRiver: return MeshColor.FromHex(0x4BB0D8);
                case AreaKind.Wetland: return MeshColor.FromHex(0x6FA9B8);
                case AreaKind.Forest: return MeshColor.FromHex(0x4E8A3A);
                case AreaKind.Farmland: return MeshColor.FromHex(0x8BD65A);
                case AreaKind.Orchard: return MeshColor.FromHex(0x7FB24A);
                case AreaKind.Meadow: return MeshColor.FromHex(0x9CC75A);
                case AreaKind.Scrub: return MeshColor.FromHex(0x8FA049);
                case AreaKind.Park: return MeshColor.FromHex(0x7FC456);
                case AreaKind.Pitch: return MeshColor.FromHex(0x5DB84A);
                case AreaKind.Grassland: return MeshColor.FromHex(0xA6C46A);
                case AreaKind.Cemetery: return MeshColor.FromHex(0x8DB86A);
                case AreaKind.TeaGarden: return MeshColor.FromHex(0x5DAE4A);
                case AreaKind.Residential: return MeshColor.FromHex(0xD2C4AA);
                case AreaKind.Commercial: return MeshColor.FromHex(0xDCC6A8);
                case AreaKind.Industrial: return MeshColor.FromHex(0xBEB7AA);
                case AreaKind.Religious: return MeshColor.FromHex(0xE0BE8C);
                case AreaKind.Pedestrian: return MeshColor.FromHex(0xCFC8BA);
                case AreaKind.SandShingle: return MeshColor.FromHex(0xE0D3B0);
                case AreaKind.BareRock:
                case AreaKind.Scree: return MeshColor.FromHex(0xA39E96);
                case AreaKind.Aerodrome: return MeshColor.FromHex(0xC9C6B8);
                case AreaKind.Glacier: return MeshColor.FromHex(0xDCEFF7);
                default: return MeshColor.FromHex(0xC9B9A0);
            }
        }
    }

    /// <summary>
    /// Water and land-use surfaces from the pre-triangulated AREA records: water kinds blue, parks and other green
    /// kinds green, built-up and bare kinds skipped (or subtle tints with <see cref="AreaOptions.IncludeSubtle"/>).
    /// Surfaces drape over the terrain at a small lift. With a <see cref="TileHeightSampler"/> every AREA triangle is
    /// clipped against the rendered terrain triangles, so the surface lies exactly the lift above the ground and
    /// shades with the terrain's own normals; with another sampler triangles are subdivided uniformly.
    /// <para>
    /// Lift (<see cref="LiftOf"/>): the family's (water over green over subtle) plus a kind rank
    /// (<see cref="AreaStyle.KindRank"/>) and a size rank (<see cref="SizeRank"/>), so where land-use polygons of one
    /// family overlap (a pitch in a park, a park in a forest) the more specific, smaller one is drawn on top
    /// instead of z-fighting. Two overlapping records of the same kind and size class remain coplanar.
    /// </para>
    /// <para>Positions are relative to the tile's south-west corner (draw areas only for exact nodes). Appends to
    /// <see cref="MeshData"/>; returns the number of area records drawn. Thread-safe for distinct meshes.</para>
    /// </summary>
    public static class AreaMesher
    {
        /// <summary>True when <see cref="Build"/> draws (and counts) the record: its family is drawn and it has
        /// triangles.</summary>
        public static bool IsDrawn(AreaRecord a, AreaOptions o)
        {
            AreaFamily f = AreaStyle.Family(a.Kind);
            if (f == AreaFamily.None || f == AreaFamily.Subtle && (o == null || !o.IncludeSubtle)) return false;
            return a.Indices != null && a.Indices.Length >= 3;
        }

        /// <summary>Lift above the terrain of a drawn record: its family's lift, plus
        /// <see cref="AreaStyle.KindRank"/> × <see cref="AreaOptions.KindLiftStepM"/>, plus <see cref="SizeRank"/> ×
        /// <see cref="AreaOptions.SizeLiftStepM"/>.</summary>
        public static float LiftOf(AreaRecord a, AreaOptions o)
        {
            if (o == null) o = new AreaOptions();
            AreaFamily f = AreaStyle.Family(a.Kind);
            float lift = f == AreaFamily.Water ? o.WaterLiftM : f == AreaFamily.Green ? o.GreenLiftM : o.SubtleLiftM;
            return lift + AreaStyle.KindRank(a.Kind) * o.KindLiftStepM + SizeRank(a) * o.SizeLiftStepM;
        }

        /// <summary>Size class of a record from the plan area of its triangles in this tile: 3 under 2 000 m²,
        /// 2 under 20 000 m², 1 under 200 000 m², else 0 (smaller draws higher). A polygon cut by a tile border is
        /// ranked per piece, so its lift may step by a millimetre or two at the border.</summary>
        public static int SizeRank(AreaRecord a)
        {
            double twice = 0;
            if (a.Indices != null && a.Vertices != null)
            {
                for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                {
                    int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                    double x0 = a.Vertices[2 * i0], z0 = a.Vertices[2 * i0 + 1];
                    double cr = (a.Vertices[2 * i1] - x0) * (a.Vertices[2 * i2 + 1] - z0) - (a.Vertices[2 * i1 + 1] - z0) * (a.Vertices[2 * i2] - x0);
                    twice += Math.Abs(cr);
                }
            }
            double m2 = twice * 0.5 / 10000.0; // cm² to m²
            return m2 < 2000 ? 3 : m2 < 20000 ? 2 : m2 < 200000 ? 1 : 0;
        }

        public static int Build(TileData t, IHeightSampler h, AreaOptions o, MeshData m)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new AreaOptions();
            var ths = h as TileHeightSampler;
            int drawn = 0;
            for (int r = 0; r < t.Areas.Count; r++)
            {
                AreaRecord a = t.Areas[r];
                if (!IsDrawn(a, o)) continue;
                float lift = LiftOf(a, o);
                uint c = AreaStyle.Rgba(a.Kind);
                int v0 = m.VertexCount;
                for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                {
                    int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                    double x0 = a.Vertices[2 * i0] / 100.0, z0 = a.Vertices[2 * i0 + 1] / 100.0;
                    double x1 = a.Vertices[2 * i1] / 100.0, z1 = a.Vertices[2 * i1 + 1] / 100.0;
                    double x2 = a.Vertices[2 * i2] / 100.0, z2 = a.Vertices[2 * i2 + 1] / 100.0;
                    double cr = (x1 - x0) * (z2 - z0) - (z1 - z0) * (x2 - x0);
                    if (Math.Abs(cr) < 1e-10) continue;
                    if (cr < 0)
                    {
                        // Robustness: the format promises counter-clockwise triangles.
                        double tx = x1, tz = z1;
                        x1 = x2;
                        z1 = z2;
                        x2 = tx;
                        z2 = tz;
                    }
                    if (ths != null && ths.HasHeights)
                        GridDrape.Triangle(t.Tile.X0, t.Tile.Z0, ths, new GridDrape.Vertex(x0, z0, 0f, 0f, c),
                                           new GridDrape.Vertex(x1, z1, 0f, 0f, c), new GridDrape.Vertex(x2, z2, 0f, 0f, c), lift, false, m);
                    else Subdivide(t, h, o, x0, z0, x1, z1, x2, z2, lift, c, m);
                }
                if (o.Detail) AreaLook.Decorate(t, a, o, m, v0);
                if (o.WaterBanks && ths != null && ths.HasHeights && AreaStyle.Family(a.Kind) == AreaFamily.Water) AreaLook.Banks(t, ths, a, o, m);
                drawn++;
            }
            if (o.FieldLines && ths != null && ths.HasHeights) AreaLook.FieldLines(t, ths, o, m);
            return drawn;
        }

        /// <summary>Fallback for generic samplers: split the triangle into k² similar triangles so no edge exceeds
        /// <see cref="AreaOptions.MaxEdgeM"/>, each vertex at the sampled height plus the lift.</summary>
        private static void Subdivide(TileData t, IHeightSampler h, AreaOptions o, double x0, double z0, double x1, double z1,
                                      double x2, double z2, float lift, uint col, MeshData m)
        {
            double maxEdge = o.MaxEdgeM > 0f ? o.MaxEdgeM : 8.0;
            double e = Math.Max(Len(x0, z0, x1, z1), Math.Max(Len(x1, z1, x2, z2), Len(x2, z2, x0, z0)));
            int k = (int)Math.Ceiling(e / maxEdge);
            if (k < 1) k = 1;
            if (k > o.MaxSubdivision) k = Math.Max(1, o.MaxSubdivision);
            int rows = k + 1;
            m.Reserve(rows * (rows + 1) / 2, 3 * k * k);
            int first = m.VertexCount;
            double size = t.Tile.Size;
            float last = 0f;
            for (int a = 0; a <= k; a++)
            {
                for (int b = 0; b <= k - a; b++)
                {
                    double fa = (double)a / k, fb = (double)b / k;
                    double x = x0 + (x1 - x0) * fa + (x2 - x0) * fb, z = z0 + (z1 - z0) * fa + (z2 - z0) * fb;
                    float y;
                    if (!h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z, out y))
                    {
                        double cx = x < 0 ? 0 : x > size ? size : x, cz = z < 0 ? 0 : z > size ? size : z;
                        if (!h.TryHeight(t.Tile.X0 + cx, t.Tile.Z0 + cz, out y)) y = last;
                    }
                    last = y;
                    float hx0, hx1, hz0, hz1;
                    if (!h.TryHeight(t.Tile.X0 + x + 1, t.Tile.Z0 + z, out hx1)) hx1 = y;
                    if (!h.TryHeight(t.Tile.X0 + x - 1, t.Tile.Z0 + z, out hx0)) hx0 = y;
                    if (!h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z + 1, out hz1)) hz1 = y;
                    if (!h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z - 1, out hz0)) hz0 = y;
                    float nx, ny, nz;
                    TileHeightSampler.FacetNormal((hx1 - (double)hx0) / 2, (hz1 - (double)hz0) / 2, out nx, out ny, out nz);
                    m.AddVertex((float)x, y + lift, (float)z, nx, ny, nz, col);
                }
            }
            // Vertex (a, b) index: rows are a = 0..k with k - a + 1 entries each.
            for (int a = 0; a < k; a++)
            {
                int rowA = first + RowStart(a, k), rowB = first + RowStart(a + 1, k);
                for (int b = 0; b < k - a; b++)
                {
                    int p = rowA + b, pb = rowA + b + 1, pa = rowB + b;
                    // (p, pa, pb) has the orientation of (v0, v1, v2): counter-clockwise -> reversed for Unity.
                    m.AddTriangle(p, pb, pa);
                    if (b < k - a - 1)
                    {
                        int pab = rowB + b + 1;
                        m.AddTriangle(pa, pb, pab);
                    }
                }
            }
        }

        private static int RowStart(int a, int k)
        {
            // Sum of (k - r + 1) for r < a.
            return a * (k + 1) - a * (a - 1) / 2;
        }

        private static double Len(double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// The detail look of the area surfaces (<see cref="AreaOptions.Detail"/>, banks and field lines): material
    /// channels and AO in UV0, world-fixed patches, water rims, banks along water edges and the terrace and bund lines
    /// of cropland, all a function of world position (tiles agree on their edges). Allocation: per call scratch only.
    /// </summary>
    internal static class AreaLook
    {
        /// <summary>Channel, AO and colour variation for the vertices an area just added (from <paramref name="v0"/>).</summary>
        public static void Decorate(TileData t, AreaRecord a, AreaOptions o, MeshData m, int v0)
        {
            if (m.VertexCount == v0) return;
            if (!m.HasUv0)
            {
                Array.Clear(m.Uv0, 0, Math.Min(m.Uv0.Length, v0 * 2));
                m.HasUv0 = true;
            }
            AreaFamily fam = AreaStyle.Family(a.Kind);
            MaterialChannel ch = ChannelOf(a.Kind);
            // Water: the distance to the shore lightens the rim (only for areas with a modest number of edges).
            double[] ex = null, ez = null;
            int edges = 0;
            if (fam == AreaFamily.Water || a.Kind == AreaKind.Park || a.Kind == AreaKind.Pitch) Boundary(a, out ex, out ez, out edges);
            if ((long)edges * (m.VertexCount - v0) > 4000000L) edges = 0;
            uint baseC = AreaStyle.Rgba(a.Kind);
            for (int v = v0; v < m.VertexCount; v++)
            {
                double x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                double wx = t.Tile.X0 + x, wz = t.Tile.Z0 + z;
                float patch = TerrainNoise.Fbm(wx, wz, 70.0, 2, 0xA4E1u), fine = TerrainNoise.Fbm(wx, wz, 19.0, 1, 0xA4E2u);
                uint c = baseC;
                float ao = 1f;
                MaterialChannel vc = ch;
                double d = edges > 0 ? Distance(ex, ez, edges, x, z) : 1e9;
                switch (fam)
                {
                    case AreaFamily.Water:
                        // Shallow light rim, deeper colour inside; a little murk on ponds.
                        float deep = (float)Math.Min(1.0, d / 7.0);
                        c = MeshColor.Lerp(MeshColor.FromHex(0x8FD0DE), baseC, 0.25f + 0.75f * deep);
                        c = MeshColor.Scale(c, 1f + 0.04f * fine);
                        break;
                    case AreaFamily.Green:
                        if (a.Kind == AreaKind.Farmland || a.Kind == AreaKind.Orchard)
                        {
                            bool bare;
                            uint crop = FieldPattern.CropColour(FieldPattern.PlotHash(wx, wz), o.Season, out bare);
                            c = MeshColor.Lerp(c, MeshColor.FromHex(crop), 0.8f);
                            if (bare) vc = MaterialChannel.Dirt;
                        }
                        else if (a.Kind == AreaKind.Forest)
                        {
                            // The floor under a closed canopy: dark, shaded green with brown leaf-litter patches.
                            uint floor = MeshColor.Lerp(MeshColor.Scale(c, 0.62f), MeshColor.FromHex(0x5C5634), 0.35f * Math.Max(0f, patch + 0.2f));
                            c = MeshColor.Lerp(floor, MeshColor.Scale(c, 0.8f), 0.35f + 0.35f * fine);
                            ao = 0.72f;
                        }
                        else
                        {
                            float dry = Math.Max(0f, Math.Min(1f, (patch - 0.2f) * 2f));
                            c = MeshColor.Lerp(c, MeshColor.FromHex(0xB8B06A), 0.22f * dry);
                            c = MeshColor.Scale(c, 1f + 0.05f * fine);
                            // A planted edge round parks and pitches: darker within 2 m of the boundary.
                            if (d < 2.5) ao = 0.78f + 0.22f * (float)(d / 2.5);
                        }
                        break;
                    default:
                        c = MeshColor.Scale(c, 1f + 0.05f * patch);
                        break;
                }
                c = (c & 0xFFFFFF00u) | 0xFFu;
                m.Colors[4 * v] = (byte)(c >> 24);
                m.Colors[4 * v + 1] = (byte)(c >> 16);
                m.Colors[4 * v + 2] = (byte)(c >> 8);
                m.Uv0[2 * v] = (float)vc;
                m.Uv0[2 * v + 1] = ao;
            }
        }

        public static MaterialChannel ChannelOf(AreaKind k)
        {
            switch (AreaStyle.Family(k))
            {
                case AreaFamily.Water: return MaterialChannel.Water;
                case AreaFamily.Green: return k == AreaKind.Forest ? MaterialChannel.Foliage : MaterialChannel.Grass;
            }
            switch (k)
            {
                case AreaKind.Pedestrian:
                case AreaKind.Religious: return MaterialChannel.Flagstone;
                case AreaKind.BareRock:
                case AreaKind.Scree: return MaterialChannel.Stone;
                case AreaKind.Commercial:
                case AreaKind.Industrial:
                case AreaKind.Aerodrome: return MaterialChannel.Concrete;
                case AreaKind.Glacier: return MaterialChannel.Plain;
                default: return MaterialChannel.Dirt;
            }
        }

        /// <summary>The boundary edges (used by one triangle) of an area, tile-local metres.</summary>
        private static void Boundary(AreaRecord a, out double[] ex, out double[] ez, out int count)
        {
            var uses = new System.Collections.Generic.Dictionary<long, int>();
            int[] idx = a.Indices;
            for (int t = 0; t + 2 < idx.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    long key = Key(idx[t + e], idx[t + (e + 1) % 3]);
                    int u;
                    uses.TryGetValue(key, out u);
                    uses[key] = u + 1;
                }
            int n = 0;
            foreach (var kv in uses)
                if (kv.Value == 1) n++;
            ex = new double[2 * n];
            ez = new double[2 * n];
            count = 0;
            for (int t = 0; t + 2 < idx.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int i0 = idx[t + e], i1 = idx[t + (e + 1) % 3];
                    if (uses[Key(i0, i1)] != 1) continue;
                    ex[2 * count] = a.Vertices[2 * i0] / 100.0;
                    ez[2 * count] = a.Vertices[2 * i0 + 1] / 100.0;
                    ex[2 * count + 1] = a.Vertices[2 * i1] / 100.0;
                    ez[2 * count + 1] = a.Vertices[2 * i1 + 1] / 100.0;
                    count++;
                }
        }

        private static long Key(int a, int b)
        {
            return a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
        }

        private static double Distance(double[] ex, double[] ez, int n, double x, double z)
        {
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double ax = ex[2 * i], az = ez[2 * i], bx = ex[2 * i + 1], bz = ez[2 * i + 1];
                double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                double px = ax + dx * f - x, pz = az + dz * f - z;
                double d2 = px * px + pz * pz;
                if (d2 < best) best = d2;
            }
            return Math.Sqrt(best);
        }

        /// <summary>A strip of gravel (rivers), sand or mud (lakes and ponds) outside every boundary edge of a water area.</summary>
        public static void Banks(TileData t, TileHeightSampler s, AreaRecord a, AreaOptions o, MeshData m)
        {
            double[] ex, ez;
            int n;
            Boundary(a, out ex, out ez, out n);
            uint c = MeshColor.FromHex(a.Kind == AreaKind.WaterRiver ? 0xB8AE94u : a.Kind == AreaKind.Wetland ? 0x7E8A58u : 0x9A8664u);
            MaterialChannel ch = a.Kind == AreaKind.WaterRiver ? MaterialChannel.Stone : MaterialChannel.Dirt;
            double w = o.BankWidthM;
            for (int i = 0; i < n; i++)
            {
                double ax = ex[2 * i], az = ez[2 * i], bx = ex[2 * i + 1], bz = ez[2 * i + 1];
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 0.2) continue;
                // Counter-clockwise triangles: the water is on the left; the bank goes out to the right.
                double nx = dz / len * w, nz = -dx / len * w;
                int v0 = m.VertexCount;
                GridDrape.Triangle(t.Tile.X0, t.Tile.Z0, s, new GridDrape.Vertex(ax, az, 0f, 0f, c), new GridDrape.Vertex(bx + nx, bz + nz, 0f, 0f, c),
                                   new GridDrape.Vertex(bx, bz, 0f, 0f, c), o.BankLiftM, false, m);
                GridDrape.Triangle(t.Tile.X0, t.Tile.Z0, s, new GridDrape.Vertex(ax, az, 0f, 0f, c), new GridDrape.Vertex(ax + nx, az + nz, 0f, 0f, c),
                                   new GridDrape.Vertex(bx + nx, bz + nz, 0f, 0f, c), o.BankLiftM, false, m);
                Channel(m, v0, ch, 0.85f, t, true);
            }
        }

        /// <summary>Set channel and AO (and a little world-fixed colour noise) on the vertices from <paramref name="v0"/>.</summary>
        private static void Channel(MeshData m, int v0, MaterialChannel ch, float ao, TileData t, bool vary)
        {
            if (!m.HasUv0)
            {
                Array.Clear(m.Uv0, 0, Math.Min(m.Uv0.Length, v0 * 2));
                m.HasUv0 = true;
            }
            for (int v = v0; v < m.VertexCount; v++)
            {
                m.Uv0[2 * v] = (float)ch;
                m.Uv0[2 * v + 1] = ao;
                if (!vary) continue;
                float f = 1f + 0.08f * TerrainNoise.Value((t.Tile.X0 + m.Positions[3 * v]) / 3.0, (t.Tile.Z0 + m.Positions[3 * v + 2]) / 3.0, 0xBA4Bu);
                for (int k = 0; k < 3; k++) m.Colors[4 * v + k] = (byte)Math.Min(255, (int)(m.Colors[4 * v + k] * f));
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Field lines: terrace risers and lips on cropland slopes, bunds between plots on the flat.

        private sealed class Scratch
        {
            public readonly double[] X = new double[16], Y = new double[16], Z = new double[16];
            public readonly double[] X2 = new double[16], Y2 = new double[16], Z2 = new double[16];
        }

        [ThreadStatic] private static Scratch _scratch;

        /// <summary>Side of the world-fixed blocks the field-line budget keeps or drops whole (metres).</summary>
        private const double BlockM = 48.0;

        /// <summary>
        /// Terrace and bund lines over the tile's farmed land. A first pass counts the triangles the risers and the
        /// bunds would take; the budget is <see cref="AreaOptions.MaxFieldLineTris"/> divided by the terrain step. Over
        /// it the bunds of the flat fields thin first (whole world-fixed <see cref="BlockM"/> blocks kept by a hash
        /// below the fitting share), then only every second terrace level is drawn (wider terraces, still at
        /// world-fixed levels), and if the risers alone still do not fit, their blocks thin the same way, so the lines
        /// thin out evenly over the tile instead of stopping at one edge.
        /// </summary>
        public static void FieldLines(TileData t, TileHeightSampler s, AreaOptions o, MeshData m)
        {
            TerrainGrid g = s.Grid;
            if (g.SubSample || o.MaxFieldLineTris <= 0) return;
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            CropLand crop = CropLand.For(t);
            int cap = o.MaxFieldLineTris / Math.Max(1, g.Step);
            int risers, bunds;
            FieldPass(t, s, o, null, crop, sc, 2f, 2f, 1, cap, out risers, out bunds);
            if (risers + bunds == 0) return;
            int stride = 1;
            float keepRisers = 2f, keepBunds = 2f;
            if (risers + bunds > cap)
            {
                if (risers > cap)
                {
                    // Recount the risers at every second level (the bunds do not change).
                    stride = 2;
                    int ignored;
                    FieldPass(t, s, o, null, crop, sc, 2f, 0f, stride, cap, out risers, out ignored);
                }
                if (risers > cap)
                {
                    keepRisers = 0.92f * cap / risers;
                    keepBunds = 0f;
                }
                else keepBunds = bunds > 0 ? 0.92f * (cap - risers) / bunds : 0f;
            }
            FieldPass(t, s, o, m, crop, sc, keepRisers, keepBunds, stride, cap, out risers, out bunds);
        }

        /// <summary>
        /// One pass over the farmed quads: with <paramref name="m"/> null counts the triangles the risers and bunds
        /// would take, else emits the lines of the blocks whose hash is below <paramref name="keepRisers"/> /
        /// <paramref name="keepBunds"/>, stopping at <paramref name="cap"/>. A quad is terraced, bunded or left wild
        /// by its smoothed slope (over about 40 m, so a terraced hillside stays terraced across small bumps and the
        /// lines do not stop at single triangles); each riser is the strip of a terrain triangle just below a terrace
        /// level, <see cref="FieldPattern.RiserPlanM"/> wide in plan (at most <see cref="FieldPattern.RiserM"/> tall).
        /// </summary>
        private static void FieldPass(TileData t, TileHeightSampler s, AreaOptions o, MeshData m, CropLand crop, Scratch sc, float keepRisers, float keepBunds,
                                      int stride, int cap, out int risers, out int bunds)
        {
            TerrainGrid g = s.Grid;
            TileData src = s.SourceTile;
            int q = g.Quads;
            double cell = g.CellM, ox = g.X0 - t.Tile.X0, oz = g.Z0 - t.Tile.Z0;
            risers = bunds = 0;
            int start = m != null ? m.TriangleCount : 0;
            bool green = o.Season == Season.Monsoon || o.Season == Season.Autumn;
            uint riser = MeshColor.FromHex(FieldPattern.RiserColour(o.Season)), lip = MeshColor.FromHex(FieldPattern.LipColour(o.Season));
            uint bund = MeshColor.FromHex(o.Season == Season.Winter ? 0xA8B070u : 0x8FB060u);
            MaterialChannel riserCh = green ? MaterialChannel.Grass : MaterialChannel.Dirt;
            double reach = Math.Max(20.0, 2.0 * cell);
            for (int k = 0; k < q; k++)
                for (int l = 0; l < q; l++)
                {
                    if (m != null && m.TriangleCount - start >= cap) return;
                    double x0 = ox + l * cell, z0 = oz + k * cell;
                    double cx = x0 + 0.5 * cell, cz = z0 + 0.5 * cell;
                    if (!Crop(t, crop, cx, cz)) continue;
                    // The quad's class from the smoothed ground slope.
                    double smooth = SmoothSlope(t, s, cx, cz, reach);
                    if (smooth > FieldPattern.TerraceMaxSlope) continue;
                    bool terraced = smooth >= FieldPattern.TerraceMinSlope;
                    float keep = terraced ? keepRisers : keepBunds;
                    if (keep <= 0f) continue;
                    if (keep < 1f && BlockHash((long)Math.Floor((t.Tile.X0 + cx) / BlockM), (long)Math.Floor((t.Tile.Z0 + cz) / BlockM)) >= keep) continue;
                    double h00 = g.VertexHeight(src, k, l), h10 = g.VertexHeight(src, k, l + 1), h01 = g.VertexHeight(src, k + 1, l), h11 = g.VertexHeight(src, k + 1, l + 1);
                    // The two triangles of the quad (split along (k, l)-(k+1, l+1), as the terrain draws them).
                    for (int half = 0; half < 2; half++)
                    {
                        double ax = x0, az = z0, ay = h00, bx, bz, by, qx = x0 + cell, qz = z0 + cell, qy = h11;
                        if (half == 0)
                        {
                            bx = x0 + cell;
                            bz = z0;
                            by = h10;
                        }
                        else
                        {
                            bx = x0;
                            bz = z0 + cell;
                            by = h01;
                        }
                        // Plane gradient of the triangle.
                        double ux = bx - ax, uz = bz - az, uy = by - ay, vx = qx - ax, vz = qz - az, vy = qy - ay;
                        double det = ux * vz - uz * vx;
                        if (Math.Abs(det) < 1e-9) continue;
                        double gx = (uy * vz - vy * uz) / det, gz = (vy * ux - uy * vx) / det;
                        double slope = Math.Sqrt(gx * gx + gz * gz);
                        float fnx, fny, fnz;
                        TileHeightSampler.FacetNormal(gx, gz, out fnx, out fny, out fnz);
                        if (terraced)
                        {
                            // A nearly level triangle inside a terraced slope has no contour to follow.
                            if (slope < 0.03) continue;
                            double ymin = Math.Min(ay, Math.Min(by, qy)), ymax = Math.Max(ay, Math.Max(by, qy));
                            double step = FieldPattern.TerraceStepM * stride;
                            double riserM = Math.Min(FieldPattern.RiserM * (stride > 1 ? 1.5 : 1.0), FieldPattern.RiserPlanM * slope);
                            double lipM = o.TerraceLips ? Math.Min(FieldPattern.LipM, 0.3 * slope) : 0.0;
                            // The riser faces downhill: its normal leans that way; the lip faces up.
                            double dl = 1.0 / slope;
                            float rnx = (float)(fnx - gx * dl * 0.9), rny = fny * 0.7f, rnz = (float)(fnz - gz * dl * 0.9);
                            float rl = (float)Math.Sqrt(rnx * rnx + rny * rny + rnz * rnz);
                            for (long lv = (long)Math.Ceiling((ymin - lipM) / step); lv * step <= ymax + riserM; lv++)
                            {
                                double level = lv * step;
                                risers += Band(sc, m, t, ax, ay, az, bx, by, bz, qx, qy, qz, 0, 0, 0, level - riserM, level, o.FieldLiftM, riser, rnx / rl, rny / rl,
                                               rnz / rl, riserCh, 0.78f);
                                if (o.TerraceLips)
                                    risers += Band(sc, m, t, ax, ay, az, bx, by, bz, qx, qy, qz, 0, 0, 0, level, level + lipM, o.FieldLiftM, lip, 0f, 1f, 0f,
                                                   MaterialChannel.Grass, 1f);
                            }
                        }
                        else
                        {
                            // Flat fields: bunds along the plot lines of the world-fixed field frame.
                            double fx, fz;
                            FieldPattern.Axis(t.Tile.X0 + cx, t.Tile.Z0 + cz, out fx, out fz);
                            for (int axis = 0; axis < 2; axis++)
                            {
                                // u = (x, z)·(fx, fz) or v = (x, z)·(-fz, fx), in world metres.
                                double dirx = axis == 0 ? fx : -fz, dirz = axis == 0 ? fz : fx, period = axis == 0 ? FieldPattern.PlotU : FieldPattern.PlotV;
                                double wa = (t.Tile.X0 + ax) * dirx + (t.Tile.Z0 + az) * dirz, wb = (t.Tile.X0 + bx) * dirx + (t.Tile.Z0 + bz) * dirz;
                                double wq = (t.Tile.X0 + qx) * dirx + (t.Tile.Z0 + qz) * dirz;
                                double lo = Math.Min(wa, Math.Min(wb, wq)), hi = Math.Max(wa, Math.Max(wb, wq));
                                for (long iu = (long)Math.Ceiling((lo - 0.3) / period); iu * period <= hi + 0.3; iu++)
                                    bunds += Band(sc, m, t, ax, ay, az, bx, by, bz, qx, qy, qz, dirx, dirz, 1, iu * period - 0.3, iu * period + 0.3, o.FieldLiftM, bund, fnx,
                                                  fny, fnz, MaterialChannel.Grass, 0.95f);
                            }
                        }
                    }
                }
        }

        /// <summary>Ground slope (rise over run) at tile-local (x, z) from differences over <paramref name="reach"/>
        /// metres on the sampler's surface (central, one-sided where a sample falls off the sampler's area).</summary>
        private static double SmoothSlope(TileData t, TileHeightSampler s, double x, double z, double reach)
        {
            double wx = t.Tile.X0 + x, wz = t.Tile.Z0 + z;
            float c;
            if (!s.TryHeight(wx, wz, out c)) return 0;
            return Math.Sqrt(Square(Diff(s, wx, wz, reach, 0, c)) + Square(Diff(s, wx, wz, 0, reach, c)));
        }

        /// <summary>Height change per metre along (dx, dz) through the centre height <paramref name="c"/>.</summary>
        private static double Diff(TileHeightSampler s, double wx, double wz, double dx, double dz, float c)
        {
            float p, n;
            bool hp = s.TryHeight(wx + dx, wz + dz, out p), hn = s.TryHeight(wx - dx, wz - dz, out n);
            double r = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (hp && hn) return (p - (double)n) / (2 * r);
            if (hp) return (p - (double)c) / r;
            if (hn) return (c - (double)n) / r;
            return 0;
        }

        private static double Square(double v)
        {
            return v * v;
        }

        /// <summary>World-fixed 0..1 hash of a field-line block.</summary>
        private static float BlockHash(long bx, long bz)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)bx) * 16777619u;
            h = (h ^ (uint)(bx >> 32)) * 16777619u;
            h = (h ^ (uint)bz) * 16777619u;
            h = (h ^ (uint)(bz >> 32)) * 16777619u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return (h & 0xFFFFFF) / 16777216f;
        }

        private static bool Crop(TileData t, CropLand crop, double x, double z)
        {
            if (crop.Farmed(x, z)) return true;
            if (t.Biomes == null || t.BiomesN < 2) return false;
            return crop.IsCrop(TerrainMesher.BiomeAt(t, t.BiomesN, t.Tile.X0 + x, t.Tile.Z0 + z), x, z);
        }

        /// <summary>
        /// The part of triangle (a, b, q) where lo ≤ f ≤ hi, f = y (<paramref name="mode"/> 0) or f = the world
        /// coordinate along (dirx, dirz) (mode 1), fanned into triangles lifted by <paramref name="lift"/>; with
        /// <paramref name="m"/> null only counts. Returns the triangles (to be) added.
        /// </summary>
        private static int Band(Scratch sc, MeshData m, TileData t, double ax, double ay, double az, double bx, double by, double bz, double qx, double qy, double qz,
                                 double dirx, double dirz, int mode, double lo, double hi, float lift, uint rgba, float nx, float ny, float nz, MaterialChannel ch, float ao)
        {
            sc.X[0] = ax; sc.Y[0] = ay; sc.Z[0] = az;
            sc.X[1] = bx; sc.Y[1] = by; sc.Z[1] = bz;
            sc.X[2] = qx; sc.Y[2] = qy; sc.Z[2] = qz;
            int n = Clip(sc, 3, mode, dirx, dirz, t, lo, true);
            if (n < 3) return 0;
            // Copy back and clip the other side.
            for (int i = 0; i < n; i++)
            {
                sc.X[i] = sc.X2[i];
                sc.Y[i] = sc.Y2[i];
                sc.Z[i] = sc.Z2[i];
            }
            n = Clip(sc, n, mode, dirx, dirz, t, hi, false);
            if (n < 3) return 0;
            if (m == null) return n - 2;
            int first = m.VertexCount, added = 0;
            for (int i = 0; i < n; i++) m.AddVertex((float)sc.X2[i], (float)sc.Y2[i] + lift, (float)sc.Z2[i], nx, ny, nz, rgba, (float)ch, ao);
            for (int i = 1; i + 1 < n; i++)
            {
                // Up-facing (Unity: clockwise seen from above): orient by the plan cross product.
                double ux = sc.X2[i] - sc.X2[0], uz = sc.Z2[i] - sc.Z2[0], vx = sc.X2[i + 1] - sc.X2[0], vz = sc.Z2[i + 1] - sc.Z2[0];
                double cr = ux * vz - uz * vx;
                if (Math.Abs(cr) < 1e-10) continue;
                if (cr > 0) m.AddTriangle(first, first + i + 1, first + i);
                else m.AddTriangle(first, first + i, first + i + 1);
                added++;
            }
            return added;
        }

        private static double F(int mode, double x, double y, double z, double dirx, double dirz, TileData t)
        {
            return mode == 0 ? y : (t.Tile.X0 + x) * dirx + (t.Tile.Z0 + z) * dirz;
        }

        /// <summary>Sutherland-Hodgman against f ≥ k (<paramref name="keepAbove"/>) or f ≤ k, from (X, Y, Z) into (X2, Y2, Z2).</summary>
        private static int Clip(Scratch sc, int n, int mode, double dirx, double dirz, TileData t, double k, bool keepAbove)
        {
            int o = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double fi = F(mode, sc.X[i], sc.Y[i], sc.Z[i], dirx, dirz, t) - k, fj = F(mode, sc.X[j], sc.Y[j], sc.Z[j], dirx, dirz, t) - k;
                if (!keepAbove)
                {
                    fi = -fi;
                    fj = -fj;
                }
                bool inI = fi >= 0, inJ = fj >= 0;
                if (inI && o < 15)
                {
                    sc.X2[o] = sc.X[i];
                    sc.Y2[o] = sc.Y[i];
                    sc.Z2[o++] = sc.Z[i];
                }
                if (inI != inJ && o < 15)
                {
                    double f = fi / (fi - fj);
                    sc.X2[o] = sc.X[i] + (sc.X[j] - sc.X[i]) * f;
                    sc.Y2[o] = sc.Y[i] + (sc.Y[j] - sc.Y[i]) * f;
                    sc.Z2[o++] = sc.Z[i] + (sc.Z[j] - sc.Z[i]) * f;
                }
            }
            return o;
        }
    }
}
