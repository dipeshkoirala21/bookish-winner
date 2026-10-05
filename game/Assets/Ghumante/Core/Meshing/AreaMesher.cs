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
                drawn++;
            }
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
}
