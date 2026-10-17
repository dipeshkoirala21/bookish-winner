using System;
using Ghumante.Core.Meshing;

namespace Ghumante.World.Buildings
{
    /// <summary>The building LOD bands of W2_DESIGN 2.4, in draw order from the camera outwards.</summary>
    public enum BuildingBandLayer : byte
    {
        /// <summary>The house grammar per detail cell (B0CellSet): each house once, at the near, lite or flat level by
        /// its distance (<see cref="BandConfig.B0Level"/>).</summary>
        B0 = 0,

        /// <summary>Styled extrusion, per 128 m block of a tile.</summary>
        B1 = 1,

        /// <summary>Simplified prisms, per 128 m block.</summary>
        B2 = 2,

        /// <summary>16 m city blocks, per 256 m block.</summary>
        B3 = 3,
    }

    /// <summary>
    /// Band radii per device tier (W2_DESIGN 2.4 table, measured horizontally from the camera) and the block sizes the
    /// streamer splits the band layers into. Each band draws [inner, outer]; the band shader dithers a
    /// <see cref="FadeM"/> wide cross-fade around every boundary (opaque, no alpha blending). Engine-free.
    /// </summary>
    public sealed class BandConfig
    {
        /// <summary>Cross-fade width at each band boundary (W2_DESIGN 2.4: 4 m, dithered).</summary>
        public const float FadeM = 4f;

        /// <summary>Block side of the B1 layer (two by two B0 cells).</summary>
        public const double B1BlockM = 128.0;

        /// <summary>Block side of the B2 layer (as fine as B1: a coarser block would push whole blocks of prisms through
        /// the vertex stage for a sliver of band).</summary>
        public const double B2BlockM = 128.0;

        /// <summary>Block side of the B3 layer.</summary>
        public const double B3BlockM = 256.0;

        /// <summary>Side of a B0 detail cell (Core BuildingDetailMesher.CellM).</summary>
        public const double CellM = 64.0;

        /// <summary>Outer radius of B0, B1, B2 and B3.</summary>
        public float B0OuterM, B1OuterM, B2OuterM, B3OuterM;

        /// <summary>A B0 house whose centroid lies within this radius is drawn at the near level (the full grammar at
        /// <see cref="B0CapTris"/> from <see cref="B0BaseDrop"/>; Core BuildingBandTable.NearOuterM). 0 on Low: no house is.</summary>
        public float B0NearOuterM;

        /// <summary>A B0 house fronting a street (<see cref="B0HouseIndex.Street"/>) whose centroid lies within this radius,
        /// beyond <see cref="B0NearOuterM"/>, is drawn at the lite level (<see cref="B0LiteDrop"/>); every other B0 house
        /// at the flat level (<see cref="B0FlatDrop"/>). 0 on Low (flat throughout).</summary>
        public float B0LiteOuterM;

        /// <summary>
        /// A house keeps a richer level until its centroid is this far past the level's radius (m), so a camera moving to
        /// and fro along a radius never makes it switch back and forth. Each house is drawn at exactly one level, in
        /// exactly one cell: the level switches when its cell is rebuilt (B0CellSet).
        /// </summary>
        public const float B0LevelHysteresisM = 4f;

        /// <summary>A B0 cell is built with the houses that meet B0 this much beyond its fade (m), around the band centre
        /// and around where its smoothed velocity takes it (<see cref="B0BuildLeadS"/>): the centre travels about
        /// <see cref="B0BuildPadM"/> − <see cref="B0LeadM"/> towards new houses before the cell is rebuilt. Smaller on Low,
        /// whose flat houses are cheap to add (a rebuild copies the houses it already has) and whose budget is tightest.</summary>
        public float B0BuildPadM;

        /// <summary>A cell is rebuilt (before anything else is built) once a house it lacks comes within this much of the
        /// band's fade (m), or within the fade around where the band centre will be in <see cref="B0NeedLeadS"/>: the build
        /// finishes before the house is drawn, so no frame lacks it. Less than <see cref="B0BuildPadM"/>.</summary>
        public float B0LeadM;

        /// <summary>Seconds of the band centre's smoothed velocity a missing house is looked for ahead (it triggers a
        /// rebuild, <see cref="B0LeadM"/>) and houses are built ahead (taken into the build, <see cref="B0BuildPadM"/>): the
        /// difference is how long a leading cell goes without a rebuild. Short on Low, whose flat houses build fast and
        /// whose 35 m band would otherwise carry a long crescent of houses beyond it.</summary>
        public float B0NeedLeadS, B0BuildLeadS;

        /// <summary>True when B0 builds 32 m cells (finer frustum culling and cheaper rebuilds around the band centre; every
        /// tier), false for 64 m cells.</summary>
        public bool B0FineCells;

        /// <summary>The triangles of all shown B0 cells this tier allows (<see cref="B0ShownBudgetTris"/>): near it, the cells
        /// shed the houses they no longer need before raising any level, and a level is raised only while the raise fits
        /// (B0CellSet.RaiseCapTris; a house that does not fit is drawn at the flat level until houses leaving make room).</summary>
        public int B0BudgetTris;

        /// <summary>GPU bytes of B0 cell meshes kept resident, shown and cached (25 / 50 / 80 MB; the cells shown at
        /// Asan take 3 / 8 / 15 MB): the least recently needed cells are evicted beyond it. With the tiles and the vehicle
        /// caches it holds the mesh memory of W2_DESIGN 10.4 (100 / 180 / 250 MB; core-tests MeshingBuildingB0CellTests:
        /// High at Basantapur, the heaviest tile set, has 141.9 MB of tile meshes).</summary>
        public long CellCacheBytes;

        /// <summary>Hero slice in triangles (W2_DESIGN 10.4: 20 k / 40 k / 60 k).</summary>
        public int HeroBudgetTris;

        /// <summary>Low never uses hero LOD0 (W2_DESIGN 3.2).</summary>
        public bool HeroLod0Allowed;

        /// <summary>B0 triangle cap per house (per plot of a row) at the near level on this tier (Core
        /// BuildingBandTable.B0CapFor).</summary>
        public int B0CapTris;

        /// <summary>The richest B0 drop level on this tier (Core BuildingBandTable.B0BaseDropFor).</summary>
        public int B0BaseDrop;

        /// <summary>The lite level (Core BuildingBandTable.LiteDrop: openings cut with their reveals and plain frames,
        /// coarse lattice, the sanjhya as a bay, eaves, shutters and boards).</summary>
        public int B0LiteDrop;

        /// <summary>The flat level (Core BuildingBandTable.FlatDrop: the same house with its openings laid on the wall).</summary>
        public int B0FlatDrop;

        /// <summary>
        /// The tier's bands: 0 Low, 1 Mid, 2 High (StreamingConfig tier numbering). Radii and caps come from the Core band
        /// table of the detail pass (BuildingBandTable): the W2 radii, and in B0 the near level within its near radius.
        /// Beyond it B0 draws the street fronts at the lite level out to <see cref="B0LiteOuterM"/> and every other house
        /// at the flat level (core-tests MeshingBuildingB0CellTests counts every shown cell at Asan and Thamel). Low has no
        /// near level and draws its 35 m flat: the W2 radius at the cheapest level that is still a house.
        /// </summary>
        public static BandConfig ForTier(int tier)
        {
            int k = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            return new BandConfig
            {
                B0OuterM = BuildingBandTable.OuterM(k, 0), B1OuterM = BuildingBandTable.OuterM(k, 1), B2OuterM = BuildingBandTable.OuterM(k, 2),
                B3OuterM = BuildingBandTable.OuterM(k, 3), B0NearOuterM = k == 0 ? 0f : BuildingBandTable.NearOuterM(k), B0LiteOuterM = LiteRadii[k],
                B0FineCells = true, CellCacheBytes = CacheMegabytes[k] << 20, HeroBudgetTris = k == 0 ? 20000 : k == 1 ? 40000 : 60000,
                HeroLod0Allowed = HeroLod0AllowedFor(k), B0CapTris = BuildingBandTable.B0CapFor(k), B0BaseDrop = BuildingBandTable.B0BaseDropFor(k),
                B0LiteDrop = BuildingBandTable.LiteDrop, B0FlatDrop = BuildingBandTable.FlatDrop, B0BuildPadM = BuildPads[k], B0LeadM = Leads[k],
                B0NeedLeadS = NeedLeads[k], B0BuildLeadS = BuildLeads[k], B0BudgetTris = B0ShownBudgetTris(k),
            };
        }

        private static readonly long[] CacheMegabytes = { 25, 50, 80 };
        private static readonly float[] LiteRadii = { 0f, 30f, 55f };
        private static readonly float[] BuildPads = { 1f, 4f, 6f };
        private static readonly float[] Leads = { 0.5f, 2f, 3f };
        private static readonly float[] NeedLeads = { 0.4f, 0.6f, 0.6f };
        private static readonly float[] BuildLeads = { 0.7f, 1.1f, 1.2f };

        /// <summary>False on Low, whose hero budget never shows LOD0 (W2_DESIGN 3.2): tile builds then skip it.</summary>
        public static bool HeroLod0AllowedFor(int tier)
        {
            return tier > 0;
        }

        /// <summary>The share of B0's triangles assumed inside the view frustum when its budget is set (W2_DESIGN 2.4: 40%).</summary>
        public const double InViewShare = 0.4;

        /// <summary>
        /// The triangles of all the B0 cells shown at once at Asan, the densest chowk, that a tier allows, every shown cell
        /// counted whole (core-tests MeshingBuildingB0CellTests): the B0 share of the building slice (Core
        /// BuildingBandTable.B0ShareTris, 40% in view) over <see cref="InViewShare"/> on Mid and High. Low keeps the W2
        /// radius (35 m) at the flat level, which at Asan's real density (about a hundred houses meet the band and its
        /// fade, where W2_DESIGN 2.4 assumed 9 in view) costs more than the table's 5.5 k in view: <see cref="LowB0ShownTris"/>
        /// (an open issue for the band table's owner, who should re-derive the Low share and slice).
        /// </summary>
        public static int B0ShownBudgetTris(int tier)
        {
            int k = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            return k == 0 ? LowB0ShownTris : (int)(BuildingBandTable.B0ShareTris(k) / InViewShare);
        }

        /// <summary>Low's B0 budget, whole shown cells (see <see cref="B0ShownBudgetTris"/>): 26.1 k at Asan at rest, and up to
        /// 31.4 k riding from Thamel to Asan at 10 m/s, at the large hotels of Dabali Marga, where the houses inside the
        /// 35 m band alone come to about 29.5 k at the flat level (core-tests MeshingBuildingB0CellTests and
        /// MeshingBuildingB0StabilityTests).</summary>
        public const int LowB0ShownTris = 33000;

        /// <summary>B0 levels: 0 near (full grammar), 1 lite, 2 flat.</summary>
        public const int LevelNear = 0, LevelLite = 1, LevelFlat = 2;

        /// <summary>
        /// The level a B0 house is drawn at, from its centroid's distance <paramref name="d"/> to the camera, whether it
        /// fronts a street and the level it is drawn at now (-1 when not built): near within <see cref="B0NearOuterM"/>,
        /// lite for a street front within <see cref="B0LiteOuterM"/>, flat otherwise; a richer level is kept until the
        /// house is <see cref="B0LevelHysteresisM"/> past its radius.
        /// </summary>
        public int B0Level(double d, bool street, int current)
        {
            int want = d < B0NearOuterM ? LevelNear : street && d < B0LiteOuterM ? LevelLite : LevelFlat;
            if (current >= 0 && current < want)
            {
                bool keep = current == LevelNear ? d < B0NearOuterM + B0LevelHysteresisM : street && d < B0LiteOuterM + B0LevelHysteresisM;
                if (keep) return current;
            }
            return want;
        }

        /// <summary>The options a B0 level builds with (<paramref name="shared"/> copied: corridors, hide set, sink).</summary>
        public BuildingOptions B0Options(int level, BuildingOptions shared)
        {
            if (level == LevelNear)
            {
                BuildingOptions o = BuildingBandTable.Copy(shared, BuildingBand.B0KitLite);
                if (B0CapTris > 0) o.B0CapTris = B0CapTris;
                o.B0BaseDrop = Math.Max(B0BaseDrop, shared != null ? shared.B0BaseDrop : 0);
                return o;
            }
            BuildingOptions lite = BuildingBandTable.LiteOptions(shared);
            lite.B0BaseDrop = level == LevelLite ? B0LiteDrop : B0FlatDrop;
            return lite;
        }

        /// <summary>Inner and outer radius a band draws (B1 with <paramref name="b1Full"/> starts at 0: its B0 cells
        /// are not ready, so it stands in for them).</summary>
        public void Range(BuildingBandLayer band, bool b1Full, out float inner, out float outer)
        {
            switch (band)
            {
                case BuildingBandLayer.B0:
                    inner = 0f;
                    outer = B0OuterM;
                    return;
                case BuildingBandLayer.B1:
                    inner = b1Full ? 0f : B0OuterM;
                    outer = B1OuterM;
                    return;
                case BuildingBandLayer.B2:
                    inner = B1OuterM;
                    outer = B2OuterM;
                    return;
                default:
                    inner = B2OuterM;
                    outer = B3OuterM;
                    return;
            }
        }

        /// <summary>
        /// True when a renderer whose horizontal bounds are [minX, maxX] × [minZ, maxZ] has any point inside the band
        /// [inner − fade/2, outer + fade/2] around the camera at (cx, cz): the conservative block test (the shader does
        /// the exact per-fragment cut).
        /// </summary>
        public static bool Touches(double minX, double minZ, double maxX, double maxZ, double cx, double cz, float inner, float outer)
        {
            double h = FadeM * 0.5;
            double dx = Math.Max(0, Math.Max(minX - cx, cx - maxX));
            double dz = Math.Max(0, Math.Max(minZ - cz, cz - maxZ));
            double near = Math.Sqrt(dx * dx + dz * dz);
            double fx = Math.Max(Math.Abs(minX - cx), Math.Abs(maxX - cx));
            double fz = Math.Max(Math.Abs(minZ - cz), Math.Abs(maxZ - cz));
            double far = Math.Sqrt(fx * fx + fz * fz);
            return near <= outer + h && far >= inner - h;
        }

        /// <summary>Nearest horizontal distance from (cx, cz) to a box.</summary>
        public static double NearestDistance(double minX, double minZ, double maxX, double maxZ, double cx, double cz)
        {
            double dx = Math.Max(0, Math.Max(minX - cx, cx - maxX));
            double dz = Math.Max(0, Math.Max(minZ - cz, cz - maxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Dither opacity of a fragment at horizontal distance <paramref name="d"/> for a band (the C# twin of
        /// the shader's GhBandFade): 1 inside, ramping linearly over <see cref="FadeM"/> centred on each edge. An
        /// inner edge at 0 never fades. The ramps of two adjacent bands sum to 1, so the cross-fade has no hole.</summary>
        public static float Opacity(double d, float inner, float outer)
        {
            double h = FadeM * 0.5;
            double a = inner <= 0f ? 1.0 : Clamp01((d - (inner - h)) / FadeM);
            double b = Clamp01(((outer + h) - d) / FadeM);
            return (float)Math.Min(a, b);
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }

    /// <summary>
    /// The point the building bands are measured from (the shader's band centre, the B0 cells' selection and the band
    /// blocks' switching; WorldStreamer.UpdateView). It follows the player (the streaming focus) while the camera rides
    /// within <see cref="ChaseRangeM"/> of it, so a chase camera swaying, bobbing or swinging round the rider never moves
    /// a band edge across a house (owner playtest #4: "Buildings constantly flickers"), and the camera otherwise (free
    /// flight, framed shots). It ignores motion within <see cref="DeadzoneM"/> (it trails the player by at most that while
    /// riding) and jumps on a teleport. Engine-free; core-tests MeshingBuildingB0StabilityTests ride it.
    /// </summary>
    public sealed class BandAnchor
    {
        /// <summary>The anchor stays put while its target moves within this distance (m).</summary>
        public const double DeadzoneM = 1.0;

        /// <summary>A camera within this distance of the focus (m) is a chase camera: the bands follow the focus.</summary>
        public const double ChaseRangeM = 30.0;

        /// <summary>A target this far from the anchor (m) is a teleport: the anchor jumps there.</summary>
        public const double JumpM = 50.0;

        /// <summary>The anchor (game metres) once <see cref="Valid"/>.</summary>
        public double X { get; private set; }

        public double Z { get; private set; }

        public bool Valid { get; private set; }

        /// <summary>Follows the focus (<paramref name="hasFocus"/>) while the camera is within <see cref="ChaseRangeM"/> of
        /// it, else the camera, with the dead zone; returns true when the anchor moved.</summary>
        public bool Follow(double camX, double camZ, double focusX, double focusZ, bool hasFocus)
        {
            double tx = camX, tz = camZ;
            if (hasFocus)
            {
                double fx = camX - focusX, fz = camZ - focusZ;
                if (fx * fx + fz * fz <= ChaseRangeM * ChaseRangeM)
                {
                    tx = focusX;
                    tz = focusZ;
                }
            }
            double dx = tx - X, dz = tz - Z, d = Math.Sqrt(dx * dx + dz * dz);
            if (!Valid || d > JumpM)
            {
                X = tx;
                Z = tz;
                Valid = true;
                return true;
            }
            if (d <= DeadzoneM) return false;
            double k = (d - DeadzoneM) / d;
            X += dx * k;
            Z += dz * k;
            return true;
        }

        /// <summary>Forgets the anchor (the next <see cref="Follow"/> jumps).</summary>
        public void Reset()
        {
            Valid = false;
        }
    }
}
