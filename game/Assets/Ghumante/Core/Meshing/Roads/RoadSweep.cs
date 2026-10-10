using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Meshing
{
    /// <summary>The surface a road piece is drawn with: albedo tint, material channel (detail-pass contract §5) and
    /// whether the crown is lightened (worn wheel tracks on asphalt and concrete; not on paving).</summary>
    public struct RoadPaving
    {
        public uint Rgba;
        public MaterialChannel Channel;
        public bool LightCrown;

        /// <summary>Heritage stone or brick paving chosen for an old-core lane (not from an OSM surface tag).</summary>
        public bool Heritage;
    }

    /// <summary>Colours, material channels and baked AO of the road kit (W2_DESIGN 4.5, docs/research/w2/ref_roads.md,
    /// detail-pass contract §5).</summary>
    public static class RoadMaterials
    {
        /// <summary>Kerb concrete (#BDB8AE).</summary>
        public static readonly uint Kerb = MeshColor.FromHex(0xBDB8AE);

        /// <summary>Painted median and island kerb bands: Kathmandu yellow and near-black.</summary>
        public static readonly uint KerbYellow = MeshColor.FromHex(0xE8C547);

        public static readonly uint KerbBlack = MeshColor.FromHex(0x2B2C2F);

        /// <summary>Soil of planting strips.</summary>
        public static readonly uint Soil = MeshColor.FromHex(0x6E4E36);

        /// <summary>Low flowering edge on planting strips.</summary>
        public static readonly uint Flowers = MeshColor.FromHex(0xD9714E);

        /// <summary>Cobbled roundabout apron.</summary>
        public static readonly uint Apron = MeshColor.FromHex(0x9A8F84);

        /// <summary>Fresh asphalt of the arterials (Ring Road, Araniko, Kanti Path).</summary>
        public static readonly uint AsphaltFresh = MeshColor.FromHex(0x4E5258);

        /// <summary>Asphalt of secondary and tertiary roads.</summary>
        public static readonly uint AsphaltCity = MeshColor.FromHex(0x5A5D62);

        /// <summary>Worn asphalt of lanes and minor roads.</summary>
        public static readonly uint AsphaltWorn = MeshColor.FromHex(0x6B6A66);

        /// <summary>Concrete road slabs.</summary>
        public static readonly uint Concrete = MeshColor.FromHex(0xA9A59C);

        /// <summary>Newar brick paving (Bhaktapur, Patan, squares; herringbone).</summary>
        public static readonly uint Brick = MeshColor.FromHex(0xA4553A);

        /// <summary>Grey stone flags of the Kathmandu core lanes (Asan, Indra Chowk).</summary>
        public static readonly uint StoneFlags = MeshColor.FromHex(0x9A968E);

        /// <summary>Retaining walls of lowered underpasses.</summary>
        public static readonly uint Retaining = MeshColor.FromHex(0xA8A49C);

        /// <summary>Median top (concrete); wide tree medians are grass (<see cref="RoadStyle.IslandGrass"/>).</summary>
        public static readonly uint MedianConcrete = MeshColor.FromHex(0xA9A59C);

        /// <summary>The material channel a road surface renders with.</summary>
        public static MaterialChannel ChannelOf(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: return MaterialChannel.Asphalt;
                case Surface.Concrete: return MaterialChannel.Concrete;
                case Surface.Brick: return MaterialChannel.Brick;
                case Surface.Cobble:
                case Surface.Rock: return MaterialChannel.Flagstone;
                case Surface.Grass: return MaterialChannel.Grass;
                case Surface.Wood: return MaterialChannel.Wood;
                case Surface.Metal: return MaterialChannel.Metal;
                case Surface.SnowIce: return MaterialChannel.Plain;
                default: return MaterialChannel.Dirt; // gravel, compacted, dirt, mud, sand, unknown
            }
        }

        /// <summary>UV0 u of a channel (contract §5: u = (float)channel).</summary>
        public static float U(MaterialChannel c)
        {
            return (float)c;
        }

        private static bool HeritageClass(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Residential:
                case RoadClass.Unclassified:
                case RoadClass.LivingStreet:
                case RoadClass.Service:
                case RoadClass.Pedestrian:
                case RoadClass.Road:
                case RoadClass.Unknown:
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True where the old core is laid in red brick (Bhaktapur, Thimi, Sankhu east of 85.37° E; Patan, Kirtipur,
        /// Bungamati and Khokana south of 27.69° N); elsewhere (the Kathmandu core) stone flags dominate.
        /// </summary>
        public static bool BrickTown(double lonDeg, double latDeg)
        {
            return lonDeg > 85.37 || latDeg < 27.69;
        }

        /// <summary>
        /// The surface of road <paramref name="road"/> (ref_roads.md §1): a tagged surface wins (asphalt by class: fresh on
        /// arterials, worn on lanes; tagged pavers in old cores as Newar brick, elsewhere red or grey interlocking pavers;
        /// setts and rock as stone flags). Untagged minor lanes in old cores get heritage paving: brick in the brick towns
        /// (<see cref="BrickTown"/>), in the Kathmandu core stone flags on 60 % of the ways and brick on the rest (by way
        /// id). Deterministic.
        /// </summary>
        public static RoadPaving PavingOf(TileData t, int road, in RoadAttrRecord a)
        {
            RoadRecord r = t.Roads[road];
            AreaType area = RoadWidthModel.AreaOf(a);
            bool tagged = r.SurfaceSource == SurfaceSource.Tagged && r.Surface != Surface.Unknown;
            bool sealedTag = tagged && (r.Surface == Surface.Asphalt || r.Surface == Surface.Concrete);
            if (area == AreaType.OldCore && HeritageClass(r.RoadClass) && !sealedTag &&
                (!tagged || r.Surface == Surface.Brick || r.Surface == Surface.Cobble || r.Surface == Surface.Rock ||
                 r.Surface == Surface.Compacted || r.Surface == Surface.Gravel))
            {
                bool brick;
                if (tagged && r.Surface == Surface.Brick) brick = true;
                else if (tagged && (r.Surface == Surface.Cobble || r.Surface == Surface.Rock)) brick = false;
                else
                {
                    int[] p = r.Points;
                    int mid = p.Length / 4;
                    double lon, lat;
                    WorldFrame.GameToLonLat(t.Tile.X0 + p[2 * mid] / 100.0, t.Tile.Z0 + p[2 * mid + 1] / 100.0, out lon, out lat);
                    brick = BrickTown(lon, lat) || RoadWidthModel.Hash(r.OsmWayId, 0x50415645) % 100 >= 60;
                }
                return new RoadPaving
                {
                    Rgba = brick ? Brick : StoneFlags, Channel = brick ? MaterialChannel.Brick : MaterialChannel.Flagstone, Heritage = !tagged,
                };
            }
            bool motor = RoadWidthModel.IsMotor(r.RoadClass) && r.RoadClass != RoadClass.Track;
            if (r.Surface == Surface.Asphalt || r.Surface == Surface.Unknown && motor)
            {
                uint c;
                switch (r.RoadClass)
                {
                    case RoadClass.Motorway:
                    case RoadClass.Trunk:
                    case RoadClass.Primary: c = AsphaltFresh; break;
                    case RoadClass.Secondary:
                    case RoadClass.Tertiary: c = AsphaltCity; break;
                    default: c = AsphaltWorn; break;
                }
                return new RoadPaving { Rgba = c, Channel = MaterialChannel.Asphalt, LightCrown = true };
            }
            switch (r.Surface)
            {
                case Surface.Concrete:
                    return new RoadPaving { Rgba = Concrete, Channel = MaterialChannel.Concrete, LightCrown = true };
                case Surface.Brick:
                    if (area == AreaType.OldCore) return new RoadPaving { Rgba = Brick, Channel = MaterialChannel.Brick };
                    return new RoadPaving { Rgba = RoadMesher.FootpathRgba(r), Channel = MaterialChannel.Brick };
                case Surface.Cobble:
                case Surface.Rock:
                    return new RoadPaving { Rgba = StoneFlags, Channel = MaterialChannel.Flagstone };
                default:
                    return new RoadPaving { Rgba = RoadStyle.SurfaceRgba(r.Surface), Channel = ChannelOf(r.Surface) };
            }
        }
    }

    /// <summary>One vertex of a cross-section row (tile-local metres, absolute Y).</summary>
    internal struct RowVertex
    {
        public double X, Z;
        public float Y;
        public float NX, NY, NZ;
        public uint Rgba;
        public MaterialChannel Channel;
        public float Ao;

        /// <summary>No strip between this vertex and the next one in the row (a hard edge: the next vertex is a duplicate
        /// with another normal or colour, or the next part is separate).</summary>
        public bool BreakAfter;
    }

    /// <summary>A cross-section row under construction (ordered left to right across the travel direction).</summary>
    internal sealed class RoadRow
    {
        public RowVertex[] V = new RowVertex[32];
        public int Count;

        public void Clear()
        {
            Count = 0;
        }

        public void Add(double x, float y, double z, float nx, float ny, float nz, uint c, MaterialChannel ch, float ao, bool breakAfter = false)
        {
            if (Count == V.Length) Array.Resize(ref V, Count * 2);
            V[Count++] = new RowVertex { X = x, Y = y, Z = z, NX = nx, NY = ny, NZ = nz, Rgba = c, Channel = ch, Ao = ao, BreakAfter = breakAfter };
        }

        /// <summary>Mark the last vertex as a hard edge.</summary>
        public void Break()
        {
            if (Count > 0) V[Count - 1].BreakAfter = true;
        }

        /// <summary>Append the vertices of another row in reverse order (an inner-to-outer side for the left).</summary>
        public void AppendReversed(RoadRow side)
        {
            for (int k = side.Count - 1; k >= 0; k--)
            {
                RowVertex v = side.V[k];
                // A break after vertex k-1 (inner order) is a break after vertex k in reversed order.
                v.BreakAfter = k > 0 && side.V[k - 1].BreakAfter;
                if (Count == V.Length) Array.Resize(ref V, Count * 2);
                V[Count++] = v;
            }
        }

        public void Append(RoadRow side, int from)
        {
            for (int k = from; k < side.Count; k++)
            {
                if (Count == V.Length) Array.Resize(ref V, Count * 2);
                V[Count++] = side.V[k];
            }
        }
    }

    /// <summary>
    /// Sweeps cross-section rows into a <see cref="MeshData"/>: consecutive rows with the same vertex count are joined by
    /// quads (rows run left to right, i.e. toward the right of the travel direction, and advance along it), each quad
    /// split along the diagonal that keeps both triangles facing the way its vertex normals do; folded triangles (tight
    /// inner curves) are re-wound, slivers dropped. Also the side treatments of the road kit: rounded edge skirts (flaring
    /// into an embankment where the road stands high), barrier kerbs with a rounded nose and raised paver footpaths,
    /// mountable painted median kerbs, shoulders, retaining walls of lowered sections, and end faces. UV0 = (channel, AO).
    /// </summary>
    internal static class RoadSweep
    {
        /// <summary>Append a row (normals normalised); returns the index of its first vertex.</summary>
        public static int Emit(MeshData m, RoadRow row)
        {
            m.Reserve(row.Count, 0);
            int first = m.VertexCount;
            for (int k = 0; k < row.Count; k++)
            {
                RowVertex v = row.V[k];
                float nx, ny, nz;
                Normalise(v.NX, v.NY, v.NZ, out nx, out ny, out nz);
                m.AddVertex((float)v.X, v.Y, (float)v.Z, nx, ny, nz, v.Rgba, RoadMaterials.U(v.Channel), Clamp01(v.Ao));
            }
            return first;
        }

        /// <summary>Unit vector of (x, y, z); straight up when it is degenerate.</summary>
        internal static void Normalise(float x, float y, float z, out float nx, out float ny, out float nz)
        {
            float l = (float)Math.Sqrt(x * x + y * y + z * z);
            if (l > 1e-6f)
            {
                nx = x / l;
                ny = y / l;
                nz = z / l;
                return;
            }
            nx = 0f;
            ny = 1f;
            nz = 0f;
        }

        internal static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>Join two emitted rows (same count) with quads, skipping hard edges of <paramref name="row"/>.</summary>
        public static void Join(MeshData m, int prev, int cur, RoadRow row)
        {
            int n = row.Count;
            m.Reserve(0, 6 * n);
            for (int k = 0; k + 1 < n; k++)
            {
                if (row.V[k].BreakAfter) continue;
                Quad(m, prev + k, cur + k, cur + k + 1, prev + k + 1);
            }
        }

        /// <summary>Quad a0 (row i, v), b0 (row i+1, v), b1 (row i+1, v+1), a1 (row i, v+1).</summary>
        public static void Quad(MeshData m, int a0, int b0, int b1, int a1)
        {
            float[] p = m.Positions, nr = m.Normals;
            double ex = nr[3 * a0] + nr[3 * b0] + nr[3 * b1] + nr[3 * a1];
            double ey = nr[3 * a0 + 1] + nr[3 * b0 + 1] + nr[3 * b1 + 1] + nr[3 * a1 + 1];
            double ez = nr[3 * a0 + 2] + nr[3 * b0 + 2] + nr[3 * b1 + 2] + nr[3 * a1 + 2];
            // Diagonal a0-b1 or b0-a1: the one whose two triangles agree better with the expected normal.
            double d1 = Math.Min(Facing(p, a0, b0, b1, ex, ey, ez), Facing(p, a0, b1, a1, ex, ey, ez));
            double d2 = Math.Min(Facing(p, a0, b0, a1, ex, ey, ez), Facing(p, b0, b1, a1, ex, ey, ez));
            if (d1 >= d2)
            {
                Tri(m, a0, b0, b1, ex, ey, ez);
                Tri(m, a0, b1, a1, ex, ey, ez);
            }
            else
            {
                Tri(m, a0, b0, a1, ex, ey, ez);
                Tri(m, b0, b1, a1, ex, ey, ez);
            }
        }

        /// <summary>A triangle wound so its front (cross(b − a, c − a), MeshingChecks.Facet) faces (ex, ey, ez); slivers
        /// are dropped.</summary>
        public static void Tri(MeshData m, int a, int b, int c, double ex, double ey, double ez)
        {
            float[] p = m.Positions;
            double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double area2 = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (area2 < 1e-7) return;
            double d = nx * ex + ny * ey + nz * ez;
            if (d >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        /// <summary>Cosine between the triangle's geometric normal and the expected one (−1 when degenerate).</summary>
        private static double Facing(float[] p, int a, int b, int c, double ex, double ey, double ez)
        {
            double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz), e = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (l < 1e-9 || e < 1e-9) return -1;
            return (nx * ex + ny * ey + nz * ez) / (l * e);
        }

        // -------------------------------------------------------------------------------------------------------
        // Side treatments (inner to outer; the caller reverses them for the left side)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Kind of edge treatment beside a carriageway or along a cap or ring kerb line.</summary>
        public enum Side : byte
        {
            /// <summary>A rounded drop from the surface edge to just under the terrain (an embankment where high).</summary>
            Skirt = 0,

            /// <summary>A barrier kerb with a rounded nose and a raised paver footpath, then a rounded drop.</summary>
            Footpath = 1,

            /// <summary>A mountable painted kerb and half of a median top (dual carriageways).</summary>
            Median = 2,

            /// <summary>An unpaved shoulder band, then a skirt.</summary>
            Shoulder = 3,

            /// <summary>Nothing beyond the edge vertex (deck edges: the bridges package draws kerbs and railings).</summary>
            None = 4,

            /// <summary>A retaining wall up to the terrain (a lowered underpass section).</summary>
            Wall = 5,
        }

        /// <summary>Number of vertices <see cref="BuildSide"/> writes for a kind (constant, so rows of one kind join).</summary>
        public static int SideCount(Side kind, bool detail)
        {
            switch (kind)
            {
                case Side.Skirt: return 2;
                case Side.Shoulder: return 4;
                case Side.Footpath: return detail ? 8 : 7;
                case Side.Median: return 6;
                case Side.Wall: return 4;
                default: return 1;
            }
        }

        /// <summary>Height under the road edge to which skirts and outer faces drop at least.</summary>
        public const float SkirtDropM = 0.12f;

        /// <summary>
        /// One side treatment, inner to outer, into <paramref name="side"/> (cleared first). The first vertex sits on the
        /// carriageway edge (ex, ez) at <paramref name="ey"/> with the carriageway colour (it is the row's edge vertex),
        /// then the treatment runs outward: points are placed at E + (ox, oz)·d for a lateral distance d (a cut section's
        /// oblique vector keeps them on the tile border line), shaded with the unit outward direction (nx, nz).
        /// <paramref name="width"/> is the footpath, half-median or shoulder width; <paramref name="paver"/> the footpath
        /// (or median top) colour. On a <paramref name="deck"/> nothing drops to the terrain.
        /// </summary>
        public static void BuildSide(RoadRow side, Side kind, bool detail, double ex, double ez, float ey, double ox, double oz, double nx, double nz,
                                     double width, uint edgeColour, MaterialChannel edgeChannel, float edgeAo, float edgeNx, float edgeNy,
                                     float edgeNz, uint paver, MaterialChannel paverChannel, RoadGrade grade, float kerbH, float medianH, bool deck)
        {
            side.Clear();
            float onx = (float)nx, onz = (float)nz;
            switch (kind)
            {
                case Side.None:
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo);
                    return;
                case Side.Skirt:
                {
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo);
                    Foot(side, grade, ex, ez, ey, ox, oz, onx, onz, edgeColour, edgeChannel, deck);
                    return;
                }
                case Side.Shoulder:
                {
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo, true);
                    float sy = ey - 0.02f;
                    side.Add(ex, sy, ez, edgeNx, edgeNy, edgeNz, RoadStyle.Shoulder, MaterialChannel.Dirt, 0.9f);
                    double sx = ex + ox * width, sz = ez + oz * width;
                    float syo = sy - 0.02f * (float)width;
                    side.Add(sx, syo, sz, edgeNx, edgeNy, edgeNz, RoadStyle.Shoulder, MaterialChannel.Dirt, 0.95f);
                    Foot(side, grade, sx, sz, syo, ox, oz, onx, onz, RoadStyle.Shoulder, MaterialChannel.Dirt, deck);
                    return;
                }
                case Side.Footpath:
                {
                    float top = ey + kerbH;
                    const double KerbTop = RoadWidthModel.KerbTopM;
                    // Edge (carriageway, AO in the gutter), then the kerb face rising toward the road with a rounded nose.
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo, true);
                    side.Add(ex, ey - 0.005f, ez, -onx, 0.05f, -onz, RoadMaterials.Kerb, MaterialChannel.Concrete, 0.6f);
                    if (detail)
                        side.Add(ex + ox * 0.03, top - 0.035f, ez + oz * 0.03, -onx * 0.7f, 0.7f, -onz * 0.7f, RoadMaterials.Kerb, MaterialChannel.Concrete, 0.95f);
                    double kx = ex + ox * KerbTop, kz = ez + oz * KerbTop;
                    side.Add(kx, top, kz, -onx * 0.1f, 1f, -onz * 0.1f, RoadMaterials.Kerb, MaterialChannel.Concrete, 1f, true);
                    // Pavers with a 2.5 % cross-fall toward the road, a rounded outer edge, then a drop.
                    side.Add(kx, top, kz, 0f, 1f, 0f, paver, MaterialChannel.Flagstone, 0.9f);
                    double w = Math.Max(KerbTop + 0.05, width);
                    double px = ex + ox * w, pz = ez + oz * w;
                    float py = top + (float)(0.025 * (w - KerbTop));
                    side.Add(px, py, pz, -onx * 0.025f, 1f, -onz * 0.025f, paver, MaterialChannel.Flagstone, 0.95f, true);
                    side.Add(px, py, pz, onx * 0.7f, 0.7f, onz * 0.7f, paver, MaterialChannel.Flagstone, 0.85f);
                    double fx = px + ox * 0.06, fz = pz + oz * 0.06;
                    float fy = deck ? py - 0.18f : Math.Min(py - 0.18f, grade.Terrain(fx, fz) - 0.08f);
                    side.Add(fx, fy, fz, onx, 0.15f, onz, paver, MaterialChannel.Flagstone, 0.5f);
                    return;
                }
                case Side.Median:
                {
                    // Mountable kerb (45° face) with a rounded crest, painted yellow, then half the median top.
                    float top = ey + medianH;
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo, true);
                    side.Add(ex, ey - 0.005f, ez, -onx * 0.7f, 0.7f, -onz * 0.7f, RoadMaterials.KerbYellow, MaterialChannel.Paint, 0.7f);
                    side.Add(ex + ox * 0.10, top - 0.01f, ez + oz * 0.10, -onx * 0.3f, 0.95f, -onz * 0.3f, RoadMaterials.KerbYellow, MaterialChannel.Paint, 1f);
                    double kx = ex + ox * 0.2, kz = ez + oz * 0.2;
                    side.Add(kx, top, kz, 0f, 1f, 0f, RoadMaterials.KerbYellow, MaterialChannel.Paint, 1f, true);
                    side.Add(kx, top + 0.01f, kz, 0f, 1f, 0f, paver, paverChannel, 0.85f);
                    double w = Math.Max(0.3, width);
                    side.Add(ex + ox * w, top + 0.03f, ez + oz * w, 0f, 1f, 0f, paver, paverChannel, 1f);
                    return;
                }
                case Side.Wall:
                {
                    side.Add(ex, ey, ez, edgeNx, edgeNy, edgeNz, edgeColour, edgeChannel, edgeAo, true);
                    float ter = grade.Terrain(ex, ez);
                    float wy = Math.Max(ter + 0.3f, ey + 0.6f);
                    side.Add(ex, ey - 0.02f, ez, -onx, 0f, -onz, RoadMaterials.Retaining, MaterialChannel.Concrete, 0.55f);
                    side.Add(ex, wy, ez, -onx, 0.2f, -onz, RoadMaterials.Retaining, MaterialChannel.Concrete, 0.9f, true);
                    side.Add(ex + ox * 0.3, wy, ez + oz * 0.3, 0f, 1f, 0f, RoadMaterials.Retaining, MaterialChannel.Concrete, 1f);
                    return;
                }
            }
        }

        /// <summary>
        /// The rounded drop beyond an outer vertex (ex, ez, ey): 0.28 m out and down to just under the terrain; where the
        /// terrain falls away (the road stands on a bench) it flares out like an embankment and takes the shoulder colour.
        /// On a deck it only drops <see cref="SkirtDropM"/>.
        /// </summary>
        private static void Foot(RoadRow side, RoadGrade grade, double ex, double ez, float ey, double ox, double oz, float onx, float onz,
                                 uint colour, MaterialChannel ch, bool deck)
        {
            double fx = ex + ox * 0.28, fz = ez + oz * 0.28;
            if (deck)
            {
                side.Add(fx, ey - SkirtDropM, fz, onx * 0.8f, 0.6f, onz * 0.8f, colour, ch, 0.55f);
                return;
            }
            float ter = grade.Terrain(fx, fz);
            float drop = ey - ter;
            if (drop > 0.4f)
            {
                // Embankment: reach out with the drop (at most 1.5 m) so the slope reads as earthwork, not a wall.
                double reach = 0.28 + Math.Min(1.2, 0.6 * (drop - 0.3));
                fx = ex + ox * reach;
                fz = ez + oz * reach;
                ter = grade.Terrain(fx, fz);
                side.Add(fx, Math.Min(ey - SkirtDropM, ter - 0.06f), fz, onx * 0.6f, 0.8f, onz * 0.6f, RoadStyle.Shoulder, MaterialChannel.Dirt, 0.6f);
                return;
            }
            side.Add(fx, Math.Min(ey - SkirtDropM, ter - 0.06f), fz, onx * 0.8f, 0.6f, onz * 0.8f, colour, ch, 0.55f);
        }

        /// <summary>
        /// Close a row's end (a footpath, median or wall stopping, or a free piece end): a fan over the vertex range
        /// [from, to] of an already emitted row (first index <paramref name="first"/>), facing along (dx, dz).
        /// </summary>
        public static void EndFace(MeshData m, int first, int from, int to, double dx, double dz)
        {
            if (to - from < 2) return;
            int a = first + from;
            for (int k = from + 1; k < to; k++)
                Tri(m, a, first + k, first + k + 1, dx, 0, dz);
        }
    }
}
