using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The looks of the Kathmandu Durbar Square temples (docs/research/heritage/kds-temples.md §3 "shared vocabulary"):
    /// the lit hex colours of the brief with their material channels. Newer tiles (Kasthamandap 2021, Chyasin Dega,
    /// Trailokya Mohan) are warmer than the weathered ones.
    /// </summary>
    internal static class KdsLooks
    {
        // Jhingati tiles (kds-temples.md "shared vocabulary"): the brief's lit colours, a shade darker as the 2023-2026 photos
        // read them under the square's haze (new tiles on Kasthamandap, Chyasin, Trailokya; weathered elsewhere).
        public static readonly ShapeBrush TileNew = Looks.Of(MeshColor.FromHex(0x8F5641), MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileOld = Looks.Of(MeshColor.FromHex(0x82533F), MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileMoss = Looks.Of(MeshColor.FromHex(0x7E6045), MaterialChannel.RoofTile);
        public static readonly ShapeBrush Valance = Looks.Of(MeshColor.FromHex(0xA9232C), MaterialChannel.Fabric);
        public static readonly ShapeBrush ValanceFold = Looks.Of(MeshColor.FromHex(0xD2555A), MaterialChannel.Fabric);
        public static readonly ShapeBrush ValanceBand = Looks.Of(MeshColor.FromHex(0xE9DCCB), MaterialChannel.Fabric);
        public static readonly ShapeBrush Strut = Looks.Of(MeshColor.FromHex(0x3B2A20), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush Timber = Looks.Of(MeshColor.FromHex(0x3E2B20), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush TimberNew = Looks.Of(MeshColor.FromHex(0x5A3A29), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush TimberRed = Looks.Of(MeshColor.FromHex(0x6B4A35), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush Beam = Looks.Of(MeshColor.FromHex(0x4A3326), MaterialChannel.Wood);
        public static readonly ShapeBrush WindowBlack = Looks.Of(MeshColor.FromHex(0x2A1E18), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush Brick = Looks.Of(MeshColor.FromHex(0xA4553D), MaterialChannel.Brick);
        public static readonly ShapeBrush BrickPlinth = Looks.Of(MeshColor.FromHex(0xA35A3C), MaterialChannel.Brick);
        public static readonly ShapeBrush BrickRed = Looks.Of(MeshColor.FromHex(0xB5502F), MaterialChannel.BrickGlazed);
        public static readonly ShapeBrush BrickOchre = Looks.Of(MeshColor.FromHex(0xC4532E), MaterialChannel.Brick);
        public static readonly ShapeBrush BrickPainted = Looks.Of(MeshColor.FromHex(0xB8483A), MaterialChannel.Paint);
        public static readonly ShapeBrush BrickBuff = Looks.Of(MeshColor.FromHex(0xB88E70), MaterialChannel.Brick);
        public static readonly ShapeBrush Nosing = Looks.Of(MeshColor.FromHex(0xBDB2A0), MaterialChannel.Stone);

        /// <summary>Plinth treads behind the stone nosing: dusty, worn brick and stone (Maju Dega, Trailokya, Chyasin photos).</summary>
        public static readonly ShapeBrush Tread = Looks.Of(MeshColor.FromHex(0x9A7F6C), MaterialChannel.Flagstone);
        public static readonly ShapeBrush Plaster = Looks.Of(MeshColor.FromHex(0xEEEBE3), MaterialChannel.Plaster);
        public static readonly ShapeBrush PlasterShade = Looks.Of(MeshColor.FromHex(0xDCD7CC), MaterialChannel.Plaster);
        public static readonly ShapeBrush PinkArch = Looks.Of(MeshColor.FromHex(0xD9A0A0), MaterialChannel.Plaster);
        public static readonly ShapeBrush StoneGrey = Looks.Of(MeshColor.FromHex(0x8C877D), MaterialChannel.Stone);
        public static readonly ShapeBrush StonePale = Looks.Of(MeshColor.FromHex(0xB9B4AA), MaterialChannel.Plain);
        public static readonly ShapeBrush StonePlinth = Looks.Of(MeshColor.FromHex(0x9C978E), MaterialChannel.Stone);
        public static readonly ShapeBrush StoneBuff = Looks.Of(MeshColor.FromHex(0xA89A86), MaterialChannel.Stone);
        public static readonly ShapeBrush StoneSoot = Looks.Of(MeshColor.FromHex(0x4A4642), MaterialChannel.Stone);
        public static readonly ShapeBrush StoneBlack = Looks.Of(MeshColor.FromHex(0x4E4B47), MaterialChannel.Plain);
        public static readonly ShapeBrush Flag = Looks.Of(MeshColor.FromHex(0x8F8B83), MaterialChannel.Flagstone);
        public static readonly ShapeBrush Gilt = Looks.Of(MeshColor.FromHex(0xD4A93A), MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltLo = Looks.Of(MeshColor.FromHex(0x9C7A26), MaterialChannel.Gilt);
        public static readonly ShapeBrush Brass = Looks.Of(MeshColor.FromHex(0xB8973F), MaterialChannel.Metal);
        public static readonly ShapeBrush BronzeDark = Looks.Of(MeshColor.FromHex(0x5B5246), MaterialChannel.Metal);
        public static readonly ShapeBrush BellBronze = Looks.Of(MeshColor.FromHex(0x6B5D45), MaterialChannel.Metal);
        public static readonly ShapeBrush Fence = Looks.Of(MeshColor.FromHex(0xA08B73), MaterialChannel.Wood);
        public static readonly ShapeBrush Grass = Looks.Of(MeshColor.FromHex(0x6F7A45), MaterialChannel.Grass);
        public static readonly ShapeBrush Rubble = Looks.Of(MeshColor.FromHex(0x9A5A42), MaterialChannel.Brick);
        public static readonly ShapeBrush MetalGrey = Looks.Of(MeshColor.FromHex(0x8A8D90), MaterialChannel.Metal);
        public static readonly ShapeBrush SheetRoof = Looks.Of(MeshColor.FromHex(0x8A3A30), MaterialChannel.Metal);
        public static readonly ShapeBrush PaintBlue = Looks.Of(MeshColor.FromHex(0x2F5DA8), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintRed = Looks.Of(MeshColor.FromHex(0xC8282E), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintWhite = Looks.Of(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintYellow = Looks.Of(MeshColor.FromHex(0xE0B437), MaterialChannel.Paint);
        public static readonly ShapeBrush BhairavBlue = Looks.Of(MeshColor.FromHex(0x22304F), MaterialChannel.Paint);
        public static readonly ShapeBrush FlameOrange = Looks.Of(MeshColor.FromHex(0xD04A1E), MaterialChannel.Paint);
        public static readonly ShapeBrush Skin = Looks.Of(MeshColor.FromHex(0xF0E3CF), MaterialChannel.Paint);
        public static readonly ShapeBrush HairBlack = Looks.Of(MeshColor.FromHex(0x1D1A18), MaterialChannel.Paint);
        public static readonly ShapeBrush ClothRed = Looks.Of(MeshColor.FromHex(0xB52A2E), MaterialChannel.Fabric);
        public static readonly ShapeBrush ClothGreen = Looks.Of(MeshColor.FromHex(0x2E7D4F), MaterialChannel.Fabric);
        public static readonly ShapeBrush ClothYellow = Looks.Of(MeshColor.FromHex(0xE8B923), MaterialChannel.Fabric);
        public static readonly ShapeBrush ClothBlue = Looks.Of(MeshColor.FromHex(0x3A5FA8), MaterialChannel.Fabric);
        public static readonly ShapeBrush Tarp = Looks.Of(MeshColor.FromHex(0xB83228), MaterialChannel.Fabric);
        public static readonly ShapeBrush Dark = Looks.Dark;
        public static readonly ShapeBrush Recess = Looks.Recess;
    }

    /// <summary>
    /// One structure being built: the mesh and colliders, the LOD, the kit frame (origin on the ground at the structure's
    /// centre, +W through the main door, U across it: to the right seen from the front), its transform, the road
    /// clearance of the tile and the stats. Positions are tile-local metres with absolute Y, like every mesher.
    /// </summary>
    internal sealed class KdsCtx
    {
        public MeshData M;
        public GenColliders C;
        public int Lod;
        public KitFrame Kf;
        public Affine3 K;
        public GenFrame F;
        public TileData Tile;
        public SacredClearance Clear;
        public SacredStats Stats;

        /// <summary>Half extents (u, w) of the structure's OSM outline in the frame (0 when it has none).</summary>
        public double PlanHu, PlanHw;

        /// <summary>The OSM outline in frame coordinates (counter-clockwise), or null.</summary>
        public double[] OutU, OutW;

        /// <summary>The tile's drawn carriageways (null without roads): ground-level colliders are clipped off them, so no
        /// plinth corner, cage or wall blocks a road as the road mesher draws it (playtest #1, #14).</summary>
        public SacredDrawnRoads Drawn;

        /// <summary>The drawn terrain (when the tile has heights).</summary>
        public RoadSurface Ground;

        public bool HasGround;

        /// <summary>The terrain height at frame (u, w), relative to the frame ground (0 without heights).</summary>
        public double GroundV(double u, double w)
        {
            if (!HasGround) return 0;
            double x, y, z;
            World(u, 0, w, out x, out y, out z);
            return Ground.Height(x, z) - F.GroundY;
        }

        public bool Lod0
        {
            get { return Lod <= 0; }
        }

        /// <summary>Draws the minor parts that follow one LOD coarser (small shrines, figures, small finials), so the
        /// site's LOD0 stays inside its budget; returns the LOD to restore with <see cref="Restore"/>.</summary>
        public int Coarser()
        {
            int was = Lod;
            Lod = Math.Min(2, Lod + 1);
            return was;
        }

        public void Restore(int lod)
        {
            Lod = lod;
        }

        /// <summary>Tile-local position of a frame point.</summary>
        public void World(double u, double v, double w, out double x, out double y, out double z)
        {
            Kf.ToWorld(u, v, w, out x, out y, out z);
        }

        /// <summary>The frame point over a tile-local (x, z).</summary>
        public void Local(double x, double z, out double u, out double w)
        {
            double dx = x - Kf.OX, dz = z - Kf.OZ;
            u = dx * Kf.UX + dz * Kf.UZ;
            w = dx * Kf.UZ - dz * Kf.UX;
        }

        /// <summary>Signed distance (m) from the frame point (u, w) to the nearest road corridor edge (negative inside);
        /// +∞ without roads.</summary>
        public double CorridorDistance(double u, double w)
        {
            if (Clear == null) return double.PositiveInfinity;
            double x, y, z;
            World(u, 0, w, out x, out y, out z);
            return Clear.Distance(x, z);
        }

        /// <summary>Signed distance (m) from the frame point (u, w) to the nearest drawn carriageway edge (negative on the
        /// road); +∞ without roads.</summary>
        public double DrawnDistance(double u, double w)
        {
            if (Drawn == null) return double.PositiveInfinity;
            double x, y, z;
            World(u, 0, w, out x, out y, out z);
            return Drawn.SignedDistance(x, z);
        }

        /// <summary>How far a ground-level collider may reach onto a drawn road (m): the drawn edge's own tolerance.</summary>
        private const double DrawnReachM = 0.05;

        /// <summary>True when a collider from v0 to v1 (frame heights) would block at the ground near (u, w).</summary>
        private bool BlocksAtGround(double u, double w, double v0, double v1)
        {
            double g = GroundV(u, w);
            return v0 < g + SacredSelector.BlockingBelowM && v1 > g + 0.1;
        }

        /// <summary>Pulls the sides of a frame rectangle in until none of them stands on a drawn road (each side moves by
        /// its worst sample's depth); false when nothing is left.</summary>
        private bool ClipOffDrawn(ref double u0, ref double u1, ref double w0, ref double w1)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                for (int s = 0; s < 4; s++)
                {
                    bool alongU = s < 2;
                    double a0 = alongU ? u0 : w0, a1 = alongU ? u1 : w1, edge = s == 0 ? w0 : s == 1 ? w1 : s == 2 ? u0 : u1;
                    int n = Math.Max(2, (int)Math.Ceiling((a1 - a0) / 0.25) + 1);
                    double worst = double.PositiveInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        double a = a0 + (a1 - a0) * i / (n - 1);
                        worst = Math.Min(worst, alongU ? DrawnDistance(a, edge) : DrawnDistance(edge, a));
                    }
                    if (worst >= -DrawnReachM) continue;
                    double d = -DrawnReachM - worst + 0.02;
                    if (s == 0) w0 += d;
                    else if (s == 1) w1 -= d;
                    else if (s == 2) u0 += d;
                    else u1 -= d;
                    moved = true;
                    if (u1 - u0 < 0.1 || w1 - w0 < 0.1) return false;
                }
                if (!moved) return true;
            }
            return u1 - u0 >= 0.1 && w1 - w0 >= 0.1;
        }

        /// <summary>A solid oriented box in frame coordinates (absolute heights from the frame ground); a box that would
        /// block at the ground is clipped off the drawn roads.</summary>
        public void Box(double u0, double u1, double w0, double w1, double v0, double v1, GenColliderFlags flags, byte material)
        {
            if (C == null || u1 <= u0 || w1 <= w0 || v1 <= v0) return;
            if (Drawn != null && BlocksAtGround(0.5 * (u0 + u1), 0.5 * (w0 + w1), v0, v1) && !ClipOffDrawn(ref u0, ref u1, ref w0, ref w1)) return;
            double x, y, z;
            World(0.5 * (u0 + u1), 0, 0.5 * (w0 + w1), out x, out y, out z);
            C.AddBox(x, z, F.GroundY + v0, F.GroundY + v1, 0.5 * (u1 - u0), 0.5 * (w1 - w0), Kf.UX, Kf.UZ, flags, material);
        }

        /// <summary>A walkable platform box (its top is ground).</summary>
        public void Floor(double u0, double u1, double w0, double w1, double v0, double v1, byte material)
        {
            Box(u0, u1, w0, w1, v0, v1, GenColliderFlags.Walkable, material);
        }

        /// <summary>A solid wall-like block the player walks round (temple walls, statues on pedestals).</summary>
        public void Solid(double u0, double u1, double w0, double w1, double v0, double v1, byte material)
        {
            Box(u0, u1, w0, w1, v0, v1, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, material);
        }

        /// <summary>A solid vertical cylinder (post, statue, column) at frame (u, w); left out where it would block a drawn
        /// road.</summary>
        public void Cylinder(double u, double w, double v0, double v1, double r, bool cameraPasses = false)
        {
            if (C == null) return;
            if (Drawn != null && BlocksAtGround(u, w, v0, v1) && DrawnDistance(u, w) < r - DrawnReachM) return;
            double x, y, z;
            World(u, 0, w, out x, out y, out z);
            C.AddCylinder(x, z, F.GroundY + v0, F.GroundY + v1, r, cameraPasses);
        }

        /// <summary>A solid thin wall (fence, parapet) between two frame points; the stretches that would block a drawn road
        /// are left out.</summary>
        public void Wall(double u0, double w0, double u1, double w1, double v0, double v1, double halfThick)
        {
            if (C == null) return;
            if (Drawn == null || !BlocksAtGround(0.5 * (u0 + u1), 0.5 * (w0 + w1), v0, v1))
            {
                AddWall(u0, w0, u1, w1, v0, v1, halfThick);
                return;
            }
            // Keep the runs of the wall whose samples stand off the drawn roads.
            double len = Math.Sqrt((u1 - u0) * (u1 - u0) + (w1 - w0) * (w1 - w0));
            int n = Math.Max(1, (int)Math.Ceiling(len / 0.2));
            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                bool clear = i < n && DrawnDistance(u0 + (u1 - u0) * (i + 0.5) / n, w0 + (w1 - w0) * (i + 0.5) / n) >= halfThick - DrawnReachM;
                if (clear && start < 0) start = i;
                if (!clear && start >= 0)
                {
                    double t0 = (double)start / n, t1 = (double)i / n;
                    AddWall(u0 + (u1 - u0) * t0, w0 + (w1 - w0) * t0, u0 + (u1 - u0) * t1, w0 + (w1 - w0) * t1, v0, v1, halfThick);
                    start = -1;
                }
            }
        }

        private void AddWall(double u0, double w0, double u1, double w1, double v0, double v1, double halfThick)
        {
            double x0, y0, z0, x1, y1, z1;
            World(u0, 0, w0, out x0, out y0, out z0);
            World(u1, 0, w1, out x1, out y1, out z1);
            C.AddWall(x0, z0, x1, z1, F.GroundY + v0, F.GroundY + v1, halfThick);
        }
    }

    /// <summary>
    /// The shared parts of the bespoke Kathmandu Durbar Square temples (kds-temples.md §5 "shared temple kit"): stepped
    /// plinth stages with stone nosings and a stair notch, stairs with sloping parapets, post rings and beam bands, walls
    /// with the black window and niche patterns on white lime plaster or brick, hipped roof tiers on measured eaves with
    /// the crimson pleated valance, struts and bells, octagonal roof tiers, and the finials (gilt gajur, white bell dome,
    /// dark bronze, white stepped pinnacle). Everything is in a kit frame (u across, v up, w out through the door) and
    /// scales its detail with the LOD (LOD1 about 30 %, LOD2 about 8 % of LOD0, silhouettes kept).
    /// </summary>
    internal static class KdsKit
    {
        public const double HalfPi = 0.5 * Math.PI;

        /// <summary>The side frame of side <paramref name="s"/> (0 = +w door side, 1 = −u, 2 = −w, 3 = +u): its local +w
        /// points out of that side and its +u runs along it.</summary>
        public static Affine3 Side(in Affine3 k, int s)
        {
            return SacredDraw.Turn(k, 0, 0, 0, s * HalfPi);
        }

        /// <summary>Half length along side s of a rectangle of half sizes (hu, hw).</summary>
        public static double Along(int s, double hu, double hw)
        {
            return s % 2 == 0 ? hu : hw;
        }

        /// <summary>Distance from the centre to side s.</summary>
        public static double Depth(int s, double hu, double hw)
        {
            return s % 2 == 0 ? hw : hu;
        }

        /// <summary>Segments of a round part at a LOD.</summary>
        public static int Seg(int lod0, int lod)
        {
            return SacredParts.Seg(lod0, lod);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Plinths and stairs
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// One rectangular plinth stage of half sizes (hu, hw) from v0 to v1: brick (or stone) faces sunk
        /// <paramref name="sink"/> under v0, a stone nosing slab of <paramref name="nose"/> m on top that overhangs by 3 cm,
        /// and the visible tread ring out to the next stage up (half sizes innerHu × innerHw; 0 caps the whole top). A
        /// stair notch of half width <paramref name="notch"/> (0 = none) leaves the front (+w) face and tread open for a
        /// stair. A walkable collider (split round the notch) at LOD0.
        /// </summary>
        public static void Stage(KdsCtx x, double hu, double hw, double v0, double v1, double innerHu, double innerHw, double notch, in ShapeBrush face,
                                 in ShapeBrush noseB, double nose, double sink, byte material, double notchBack = 0)
        {
            MeshData m = x.M;
            Affine3 k = x.K;
            int lod = x.Lod;
            nose = Math.Min(nose, 0.6 * (v1 - v0));
            double lip = lod <= 1 && nose > 0.02 ? 0.03 : 0;
            double fTop = v1 - nose;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = Side(k, s);
                double a = Along(s, hu, hw), d = Depth(s, hu, hw);
                bool cut = s == 0 && notch > 0;
                if (cut)
                {
                    SacredDraw.Panel(m, sk, -a, v0 - sink, -notch, fTop, d, face);
                    SacredDraw.Panel(m, sk, notch, v0 - sink, a, fTop, d, face);
                    if (nose > 0.005)
                    {
                        SacredDraw.Panel(m, sk, -a - lip, fTop, -notch, v1, d + lip, noseB);
                        SacredDraw.Panel(m, sk, notch, fTop, a + lip, v1, d + lip, noseB);
                    }
                }
                else
                {
                    SacredDraw.Panel(m, sk, -a, v0 - sink, a, fTop, d, face);
                    if (nose > 0.005) SacredDraw.Panel(m, sk, -a - lip, fTop, a + lip, v1, d + lip, noseB);
                }
                if (lip > 0 && nose > 0.005)
                {
                    // The nosing's underside lip.
                    SacredDraw.Quad(m, sk, -a - lip, fTop, d, a + lip, fTop, d, a + lip, fTop, d + lip, -a - lip, fTop, d + lip, 0, -1, 0, Looks.Ao(noseB, 0.6f));
                }
            }
            // Tread: the ring between this stage and the next (or the whole top).
            double ihu = Math.Max(0, Math.Min(innerHu, hu)), ihw = Math.Max(0, Math.Min(innerHw, hw));
            // A buff stone nosing reads as a pale line at each step edge; the tread behind it is worn brick and dust.
            ShapeBrush top = noseB.Color == KdsLooks.Nosing.Color ? KdsLooks.Tread : noseB;
            double eu = hu + lip, ew = hw + lip;
            if (ihu <= 0 || ihw <= 0)
            {
                if (notch > 0)
                {
                    SacredDraw.Quad(m, k, -eu, v1, -ew, eu, v1, -ew, eu, v1, notchBack, -eu, v1, notchBack, 0, 1, 0, top);
                    SacredDraw.Quad(m, k, -eu, v1, notchBack, -notch, v1, notchBack, -notch, v1, ew, -eu, v1, ew, 0, 1, 0, top);
                    SacredDraw.Quad(m, k, notch, v1, notchBack, eu, v1, notchBack, eu, v1, ew, notch, v1, ew, 0, 1, 0, top);
                }
                else
                {
                    SacredDraw.CapRect(m, k, 0, 0, eu, ew, v1, true, top);
                }
            }
            else
            {
                // Back, left, right strips and the front split by the notch.
                SacredDraw.Quad(m, k, -eu, v1, -ew, eu, v1, -ew, eu, v1, -ihw, -eu, v1, -ihw, 0, 1, 0, top);
                SacredDraw.Quad(m, k, -eu, v1, -ihw, -ihu, v1, -ihw, -ihu, v1, ihw, -eu, v1, ihw, 0, 1, 0, top);
                SacredDraw.Quad(m, k, ihu, v1, -ihw, eu, v1, -ihw, eu, v1, ihw, ihu, v1, ihw, 0, 1, 0, top);
                if (notch > 0)
                {
                    SacredDraw.Quad(m, k, -eu, v1, ihw, -notch, v1, ihw, -notch, v1, ew, -eu, v1, ew, 0, 1, 0, top);
                    SacredDraw.Quad(m, k, notch, v1, ihw, eu, v1, ihw, eu, v1, ew, notch, v1, ew, 0, 1, 0, top);
                }
                else
                {
                    SacredDraw.Quad(m, k, -eu, v1, ihw, eu, v1, ihw, eu, v1, ew, -eu, v1, ew, 0, 1, 0, top);
                }
            }
            if (x.C != null)
            {
                if (notch > 0)
                {
                    x.Floor(-hu, -notch, -hw, hw, v0 - sink, v1, material);
                    x.Floor(notch, hu, -hw, hw, v0 - sink, v1, material);
                    double back = Math.Min(hw, notchBack);
                    if (back > -hw) x.Floor(-notch, notch, -hw, back, v0 - sink, v1, material);
                }
                else
                {
                    x.Floor(-hu, hu, -hw, hw, v0 - sink, v1, material);
                }
            }
        }

        /// <summary>A plain rectangular stage (see <see cref="Stage"/>) centred at frame (cu, cw) instead of the origin.</summary>
        public static void StageAt(KdsCtx x, double cu, double cw, double hu, double hw, double v0, double v1, in ShapeBrush face, in ShapeBrush noseB, double nose,
                                   double sink, byte material)
        {
            KitFrame kf = x.Kf;
            Affine3 k = x.K;
            x.Kf = kf.Offset(cu, 0, cw);
            x.K = SacredDraw.Xf(x.Kf);
            Stage(x, hu, hw, v0, v1, 0, 0, 0, face, noseB, nose, sink, material);
            x.Kf = kf;
            x.K = k;
        }

        /// <summary>
        /// A straight stair on the front (+w) from the top edge <paramref name="wTop"/> (height v1) down to the foot at
        /// wTop + run (height v0), half width <paramref name="halfW"/>, flanked by sloping parapet walls of width
        /// <paramref name="sideW"/> standing <paramref name="sideH"/> over the treads (0 = none); a walkable ramp collider.
        /// </summary>
        public static int Stair(KdsCtx x, double halfW, double wTop, double run, double v0, double v1, double sideW, double sideH, in ShapeBrush step,
                                in ShapeBrush side, int steps = 0, byte material = GenColliders.Stone)
        {
            int lod = x.Lod;
            return SacredParts.Stair(x.M, x.C, x.Kf, x.K, halfW, wTop, run, v0, v1, sideW, sideH, step, side, lod, material,
                                     lod <= 1 ? steps : Math.Max(1, steps / 3));
        }

        /// <summary>A stair on side <paramref name="s"/> of the frame (0 = front): the frame is turned so the stair runs
        /// out of that side; the stair is centred at <paramref name="along"/> on it.</summary>
        public static void SideStair(KdsCtx x, int s, double along, double halfW, double wTop, double run, double v0, double v1, double sideW, double sideH,
                                     in ShapeBrush step, in ShapeBrush side, byte material = GenColliders.Stone)
        {
            KitFrame kf = x.Kf;
            // TurnedRight makes the new W the old −U: side s (0 +w, 1 −u, 2 −w, 3 +u) is s right turns.
            for (int q = 0; q < s; q++) kf = kf.TurnedRight();
            kf = kf.Offset(along, 0, 0);
            Affine3 k = SacredDraw.Xf(kf);
            SacredParts.Stair(x.M, x.C, kf, k, halfW, wTop, run, v0, v1, sideW, sideH, step, side, x.Lod, material);
        }

        /// <summary>A plinth level on an arbitrary outline (frame coordinates, e.g. the OSM footprint itself): a proud base
        /// course, the face and a coping (or plain at LOD2), capped; a walkable collider over its bounding box.</summary>
        public static void OutlineLevel(KdsCtx x, double[] pu, double[] pw, int n, double v0, double v1, double sink, in ShapeBrush face, in ShapeBrush coping,
                                        byte material)
        {
            SacredParts.PlinthLevel(x.M, x.K, pu, pw, n, v0, v1, sink, face, coping, x.Lod);
            if (x.C == null) return;
            double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                u0 = Math.Min(u0, pu[i]);
                u1 = Math.Max(u1, pu[i]);
                w0 = Math.Min(w0, pw[i]);
                w1 = Math.Max(w1, pw[i]);
            }
            x.Floor(u0, u1, w0, w1, v0 - sink, v1, material);
        }

        /// <summary>A rectangle outline (counter-clockwise) into scratch arrays; returns 4.</summary>
        public static int Rect(double hu, double hw, double[] pu, double[] pw)
        {
            return SacredDraw.Rect(0, 0, hu, hw, pu, pw);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Posts, beams, walls, openings
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A ring of carved posts on a rectangle of half sizes (hu, hw) from v0 to v1: the corners plus
        /// <paramref name="perU"/> − 1 / <paramref name="perW"/> − 1 between them on each side (bays per side); solid
        /// cylinders at LOD0. At LOD2 only the corners.</summary>
        public static void PostRing(KdsCtx x, double hu, double hw, double v0, double v1, int perU, int perW, double t, in ShapeBrush wood)
        {
            int lod = x.Lod;
            for (int s = 0; s < 4; s++)
            {
                int bays = s % 2 == 0 ? perU : perW;
                if (lod >= 2) bays = 1;
                double a = Along(s, hu, hw);
                for (int q = 0; q < bays; q++)
                {
                    double f = -a + 2 * a * q / bays;
                    double u, w;
                    SacredParts.OnRect(s, f / Math.Max(1e-9, a), hu, hw, out u, out w);
                    Post(x, u, w, v0, v1, t, wood);
                }
            }
        }

        /// <summary>One carved post at (u, w) from v0 to v1 (moulded base, shaft, carved capital band, bracket at LOD0).</summary>
        public static void Post(KdsCtx x, double u, double w, double v0, double v1, double t, in ShapeBrush wood)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double h = 0.5 * t;
            if (lod >= 1)
            {
                // Beyond the nearest LOD the base block and capital band no longer read: a plain square post.
                SacredDraw.Box(m, x.K, u - h, u + h, v0, v1, w - h, w + h, wood, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                if (lod == 1) x.Cylinder(u, w, v0, v1, h * 1.1, true);
                return;
            }
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(u, w, h, h, pu, pw);
            Mould p = SacredDraw.M;
            double len = v1 - v0;
            if (lod == 0)
            {
                // Base block, shaft, the carved capital band (a lotus ring) and the flared head.
                p.Add(0.06, v0, wood).Add(0.06, v0 + 0.24).Add(0, v0 + 0.3).Add(0, v1 - 0.5).Add(0.04, v1 - 0.44, Looks.Ao(wood, 0.85f)).Add(0.03, v1 - 0.14, wood)
                 .Add(0.05, v1);
            }
            else
            {
                p.Add(0.05, v0, wood).Add(0.05, v0 + 0.22).Add(0, v0 + 0.22).Add(0, v1);
            }
            SacredDraw.Ring(m, x.K, pu, pw, n, p);
            if (lod == 0 && len > 1.8)
            {
                // The saddle bracket under the beam.
                SacredDraw.Box(m, x.K, u - 2.6 * h, u + 2.6 * h, v1 - 0.02, v1 + 0.1, w - 1.05 * h, w + 1.05 * h, Looks.Ao(wood, 0.9f), BoxFaces.All & ~BoxFaces.Top);
            }
            x.Cylinder(u, w, v0, v1, h * 1.1, true);
        }

        /// <summary>A rectangular beam band (a ring of four boxes) of half sizes (hu, hw) from v0 to v1, depth d inward.</summary>
        public static void BeamRing(KdsCtx x, double hu, double hw, double v0, double v1, double d, in ShapeBrush b)
        {
            Mould p = SacredDraw.M;
            p.Add(0, v0, b).Add(0, v1);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
            if (x.Lod >= 2) return;
            // Underside.
            SacredDraw.Quad(x.M, x.K, -hu, v0, -hw, hu, v0, -hw, hu, v0, -hw + d, -hu, v0, -hw + d, 0, -1, 0, Looks.Ao(b, 0.6f));
            SacredDraw.Quad(x.M, x.K, -hu, v0, hw - d, hu, v0, hw - d, hu, v0, hw, -hu, v0, hw, 0, -1, 0, Looks.Ao(b, 0.6f));
            SacredDraw.Quad(x.M, x.K, -hu, v0, -hw + d, -hu + d, v0, -hw + d, -hu + d, v0, hw - d, -hu, v0, hw - d, 0, -1, 0, Looks.Ao(b, 0.6f));
            SacredDraw.Quad(x.M, x.K, hu - d, v0, -hw + d, hu, v0, -hw + d, hu, v0, hw - d, hu - d, v0, hw - d, 0, -1, 0, Looks.Ao(b, 0.6f));
        }

        /// <summary>A carved cornice band round a rectangle: stepped timber mouldings projecting up to <paramref name="out"/>.</summary>
        public static void Cornice(KdsCtx x, double hu, double hw, double v0, double v1, double outM, in ShapeBrush b)
        {
            Mould p = SacredDraw.M;
            double h = v1 - v0;
            if (x.Lod >= 2) p.Add(outM, v0, b).Add(outM, v1);
            else
                p.Add(0, v0, Looks.Ao(b, 0.8f)).Add(0.35 * outM, v0 + 0.1 * h).Add(0.35 * outM, v0 + 0.4 * h, b).Add(0.7 * outM, v0 + 0.45 * h)
                 .Add(0.7 * outM, v0 + 0.75 * h).Add(outM, v0 + 0.8 * h).Add(outM, v1).Add(0, v1, Looks.Ao(b, 0.7f));
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
        }

        /// <summary>A wall block of half sizes (hu, hw) from v0 to v1 (no top), optionally with a sill beam and a cornice
        /// (see <see cref="SacredParts.BandedWall"/>), and a solid collider.</summary>
        public static void Walls(KdsCtx x, double hu, double hw, double v0, double v1, in ShapeBrush wall, bool sill, bool cornice, byte material = GenColliders.Brick)
        {
            SacredParts.BandedWall(x.M, x.K, hu, hw, v0, v1, wall, KdsLooks.Timber, x.Lod, sill, cornice);
            x.Solid(-hu, hu, -hw, hw, v0, v1, material);
        }

        /// <summary>
        /// A black carved window on a wall plane (side frame sk, plane w = d): the recessed dark panel, a frame whose sill
        /// and lintel run past the jambs as ears, and lattice bars at LOD0 (tiki jhyā). <paramref name="tEars"/> widens the
        /// lintel ears into the T-shaped black pattern of Maju Dega's and Trailokya's upper storeys.
        /// </summary>
        public static void BlackWindow(MeshData m, in Affine3 sk, double u, double v, double d, double ww, double wh, int lod, double tEars = 0.12, int bars = 3)
        {
            double fw = 0.08, hu = 0.5 * ww, hv = 0.5 * wh;
            ShapeBrush frame = KdsLooks.WindowBlack;
            if (lod >= 1)
            {
                // Far off the frame's relief no longer reads: the dark panel inside a flat black frame (jamb band, lintel
                // and sill with their ears) standing just off the wall.
                SacredDraw.Panel(m, sk, u - hu, v - hv, u + hu, v + hv, d + 0.04, Looks.Recess);
                if (lod >= 2) return;
                SacredDraw.Panel(m, sk, u - hu - fw, v - hv, u + hu + fw, v + hv, d + 0.02, frame);
                SacredDraw.Panel(m, sk, u - hu - fw - tEars, v + hv, u + hu + fw + tEars, v + hv + 1.4 * fw, d + 0.02, frame);
                SacredDraw.Panel(m, sk, u - hu - fw - tEars, v - hv - fw, u + hu + fw + tEars, v - hv, d + 0.02, frame);
                return;
            }
            SacredDraw.Panel(m, sk, u - hu, v - hv, u + hu, v + hv, d + 0.01, Looks.Recess);
            // Jambs (their tops hide under the lintel), the lintel with its ears and the sill.
            SacredDraw.Box(m, sk, u - hu - fw, u - hu, v - hv, v + hv, d, d + 0.07, frame, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, sk, u + hu, u + hu + fw, v - hv, v + hv, d, d + 0.07, frame, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, sk, u - hu - fw - tEars, u + hu + fw + tEars, v + hv, v + hv + 1.4 * fw, d, d + 0.08, frame, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            SacredDraw.Box(m, sk, u - hu - fw - tEars, u + hu + fw + tEars, v - hv - fw, v - hv, d, d + 0.08, frame, BoxFaces.Front | BoxFaces.Top);
            double bw = 0.022;
            // Small windows keep two bars each way (the lattice still reads at two thirds the cost); the smallest, high
            // up under the eaves, only their dark panel and frame.
            if (ww * wh < 0.22) return;
            if (ww < 0.7 && wh < 0.7) bars = Math.Min(bars, 2);
            // The lattice bars as flat strips standing a little proud of the dark panel.
            for (int i = 1; i <= bars; i++)
            {
                double uu = u - hu + ww * i / (bars + 1), vv = v - hv + wh * i / (bars + 1);
                SacredDraw.Panel(m, sk, uu - bw, v - hv, uu + bw, v + hv, d + 0.035, frame);
                SacredDraw.Panel(m, sk, u - hu, vv - bw, u + hu, vv + bw, d + 0.03, frame);
            }
        }

        /// <summary>A black keyhole-shaped blind niche (Trailokya Mohan's sanctum walls) centred at u with its foot at v on
        /// the plane w = d of side frame sk, total height h: a round head over a tapering body, in a dark carved frame.</summary>
        public static void KeyholeNiche(MeshData m, in Affine3 sk, double u, double v, double d, double h, int lod)
        {
            double r = 0.22 * h, body = h - 2 * r;
            int seg = lod == 0 ? 12 : lod == 1 ? 8 : 5;
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int n = 0;
            // Body: a trapezoid narrowing upward; head: a circle on top (polygon in the wall plane: x = u, z = −v).
            px[n] = -0.75 * r;
            pz[n++] = 0;
            px[n] = 0.75 * r;
            pz[n++] = 0;
            px[n] = 0.38 * r;
            pz[n++] = -body;
            for (int i = 0; i <= seg; i++)
            {
                double a = -0.5 * Math.PI + 0.2 + (2 * Math.PI - 0.4) * i / seg;
                px[n] = r * Math.Cos(a) * -1;
                pz[n++] = -(body + r + r * Math.Sin(-a));
            }
            px[n] = -0.38 * r;
            pz[n++] = -body;
            Affine3 f = SacredDraw.Basis(sk, u, v, d, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, f, KdsLooks.WindowBlack, px, pz, n, lod <= 1 ? 0.06 : 0.03, lod == 0 ? 0.015 : 0, lod == 0 ? 1 : 0,
                                BevelStyle.Chamfer, true, false);
        }

        /// <summary>A closed sanctum door on side frame sk at the wall plane d (dark doorway, carved frame, torana).</summary>
        public static double Door(MeshData m, in Affine3 sk, double u, double v, double d, double dw, double dh, in ShapeBrush frame, int lod, bool torana,
                                  double maxTop = double.MaxValue)
        {
            return SacredParts.Door(m, sk, u, v, d, dw, dh, frame, lod, torana, maxTop);
        }

        /// <summary>A carved lattice door (the closed doors of Trailokya Mohan and Maju Dega): dark frame, two leaves with
        /// a diagonal lattice at LOD0.</summary>
        public static void LatticeDoor(MeshData m, in Affine3 sk, double u, double v, double d, double dw, double dh, int lod)
        {
            SacredDraw.Panel(m, sk, u - 0.5 * dw, v, u + 0.5 * dw, v + dh, d + 0.012, Looks.Dark);
            if (lod >= 2) return;
            ShapeBrush fr = KdsLooks.WindowBlack;
            double jw = 0.12;
            SacredDraw.Box(m, sk, u - 0.5 * dw - jw, u - 0.5 * dw, v, v + dh + jw, d, d + 0.1, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + 0.5 * dw, u + 0.5 * dw + jw, v, v + dh + jw, d, d + 0.1, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u - 0.5 * dw - jw - 0.25, u + 0.5 * dw + jw + 0.25, v + dh, v + dh + jw, d, d + 0.12, fr, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Box(m, sk, u - 0.5 * dw - jw - 0.2, u + 0.5 * dw + jw + 0.2, v - 0.04, v + 0.08, d, d + 0.12, fr, BoxFaces.Wall);
            if (lod >= 1) return;
            ShapeBrush lat = Looks.Of(MeshColor.FromHex(0x4A3528), MaterialChannel.WoodCarved);
            // Lattice: diagonal slats as thin boxes in two directions, framed in each leaf.
            int rows = Math.Max(3, (int)(dh / 0.28));
            for (int leaf = -1; leaf <= 1; leaf += 2)
            {
                double c = u + leaf * 0.25 * dw;
                for (int r = 0; r < rows; r++)
                {
                    double vv = v + 0.15 + (dh - 0.3) * (r + 0.5) / rows;
                    SacredDraw.Box(m, sk, c - 0.22 * dw, c + 0.22 * dw, vv - 0.018, vv + 0.018, d + 0.012, d + 0.04, lat, BoxFaces.Front | BoxFaces.Top);
                }
                for (int q = -1; q <= 1; q++)
                {
                    double uu = c + q * 0.14 * dw;
                    SacredDraw.Box(m, sk, uu - 0.018, uu + 0.018, v + 0.12, v + dh - 0.12, d + 0.012, d + 0.042, lat, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Roofs
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A hipped roof tier on measured numbers (half sizes of the eave edge at <paramref name="eaveV"/>, the ring where
        /// the slope meets the storey above or closes at the apex, the wall the soffit returns to) in jhingati tiles (or gilt
        /// copper, in the brush given), with gently lifted corners (or the curled corner horns of a gilt shrine roof when
        /// <paramref name="horns"/>) and the KDS crimson pleated valance of height <paramref name="valH"/> (0 = none).
        /// Returns the spec for struts and bells.
        /// </summary>
        public static RoofSpec Roof(KdsCtx x, double eaveHu, double eaveHw, double eaveV, double topHu, double topHw, double topV, double wallHu, double wallHw,
                                    double wallTopV, in ShapeBrush tile, double valH, bool gilt = false, double cornerLift = -1, ShapeBrush? fascia = null,
                                    ShapeBrush? ridge = null, bool horns = false)
        {
            double big = Math.Max(eaveHu, eaveHw), fasciaH = Math.Min(0.24, 0.1 + 0.012 * big);
            // Over a road corridor the eave edge (fascia bottom) keeps the overhead clearance (contract §1): the tier is
            // raised as a whole as far as needed; the slope above gets a little flatter.
            double lift = FitEave(x, ref eaveHu, ref eaveHw, wallHu, wallHw, eaveV - fasciaH);
            if (lift > 0)
            {
                eaveV += lift;
                if (topV < eaveV + 0.3) topV = eaveV + 0.3;
                wallTopV = Math.Max(wallTopV, eaveV + 0.05);
            }
            var r = new RoofSpec
            {
                EaveU = eaveHu, EaveW = eaveHw, EaveV = eaveV, TopU = Math.Max(0.08, topHu), TopW = Math.Max(0.08, topHw), TopV = topV, WallU = wallHu, WallW = wallHw,
                WallTopV = wallTopV, Lift = cornerLift >= 0 ? cornerLift : 0.035 * big, Sag = 0.018 * Math.Min(eaveHu, eaveHw), FasciaH = fasciaH,
                // No curled horns by default: the Kathmandu square's tile eaves end in a gently lifted corner (the photos),
                // not the tall kunsala curl of the generic pagodas; the gilt shrine roofs (Ashok Binayak) curl.
                CoursePitch = big >= 6.0 ? 1.3 : 0, Gilt = gilt, Fringe = false, FringeH = valH, Horns = horns, Clear = x.Clear, GroundY = x.F.GroundY,
                Roof = tile,
                Ridge = ridge ?? (gilt ? Looks.GiltHi : Looks.Of(MeshColor.Scale(tile.Color, 0.7f), MaterialChannel.RoofTile)),
                Fascia = fascia ?? (gilt ? Looks.GiltAged : KdsLooks.Timber), Soffit = Looks.Ao(KdsLooks.Timber, 0.55f), Horn = gilt ? tile : KdsLooks.Timber,
            };
            SacredParts.Roof(x.M, x.K, r, x.Lod);
            if (valH > 0.01) Valance(x, r, valH);
            if (x.C != null)
            {
                // The roof is a solid the camera and player do not pass (above head height).
                x.Box(-wallHu, wallHu, -wallHw, wallHw, eaveV, Math.Max(eaveV + 0.3, topV), GenColliderFlags.NoClimb, GenColliders.Wood);
            }
            return r;
        }

        /// <summary>
        /// Fits a rectangle of the frame (centre cu, cw; half sizes hu, hw) out of the road corridors (contract §1: rideability
        /// beats exact footprints): every side whose samples come nearer a corridor than the margin moves inward by the
        /// deficit (the opposite side stays), down to the minimum half sizes. Rest houses and pavilions use it; temples keep
        /// their measured plans.
        /// </summary>
        public static void FitRect(KdsCtx x, ref double cu, ref double cw, ref double hu, ref double hw, double minHu, double minHw)
        {
            if (x.Clear == null) return;
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                for (int s = 0; s < 4; s++)
                {
                    double worst = double.MaxValue;
                    for (int q = 0; q <= 8; q++)
                    {
                        double u, w;
                        SacredParts.OnRect(s, -1 + q / 4.0, hu, hw, out u, out w);
                        worst = Math.Min(worst, x.CorridorDistance(cu + u, cw + w));
                    }
                    double need = SacredClearance.MarginM + 0.05 - worst;
                    if (need <= 0) continue;
                    double ou, ow;
                    SacredParts.Out(s, out ou, out ow);
                    if (ou != 0)
                    {
                        double d = Math.Min(need, 2 * (hu - minHu));
                        if (d <= 0.01) continue;
                        hu -= 0.5 * d;
                        cu -= 0.5 * d * ou;
                    }
                    else
                    {
                        double d = Math.Min(need, 2 * (hw - minHw));
                        if (d <= 0.01) continue;
                        hw -= 0.5 * d;
                        cw -= 0.5 * d * ow;
                    }
                    moved = true;
                }
                if (!moved) return;
            }
        }

        /// <summary>
        /// A roof tier whose overhang is fitted side by side to the road corridors (an eave lower than the overhead clearance
        /// is pulled back on the sides over a corridor only, down to none): the tier is drawn about a shifted centre with
        /// its top ring and soffit widened by the shift so they still meet the storey above and the wall. Struts are
        /// skipped when the eave had to be made asymmetric. Same arguments as <see cref="Roof"/> (frame centred).
        /// </summary>
        public static RoofSpec RoofFit(KdsCtx x, double eaveHu, double eaveHw, double eaveV, double topHu, double topHw, double topV, double wallHu,
                                       double wallHw, double wallTopV, in ShapeBrush tile, double valH, out bool asymmetric)
        {
            asymmetric = false;
            double big = Math.Max(eaveHu, eaveHw), fasciaH = Math.Min(0.24, 0.1 + 0.012 * big), edgeV = eaveV - fasciaH;
            var o = new double[4];
            for (int s = 0; s < 4; s++)
            {
                double full = Math.Max(0, s % 2 == 0 ? eaveHw - wallHw : eaveHu - wallHu);
                o[s] = 0;
                for (int i = 0; i <= 7; i++)
                {
                    double ov = full * (1 - i / 7.0);
                    if (SideLift(x, s, wallHu + (s % 2 == 1 ? ov : 0), wallHw + (s % 2 == 0 ? ov : 0), edgeV, s % 2 == 0 ? eaveHu : eaveHw) > 0) continue;
                    o[s] = ov;
                    break;
                }
            }
            // Sides: 0 +w, 1 −u, 2 −w, 3 +u.
            double du = 0.5 * (o[3] - o[1]), dw = 0.5 * (o[0] - o[2]);
            asymmetric = Math.Abs(du) > 0.05 || Math.Abs(dw) > 0.05;
            double eu = wallHu + 0.5 * (o[1] + o[3]), ew = wallHw + 0.5 * (o[0] + o[2]);
            KitFrame kf = x.Kf;
            Affine3 k = x.K;
            x.Kf = kf.Offset(du, 0, dw);
            x.K = SacredDraw.Xf(x.Kf);
            RoofSpec r = Roof(x, eu, ew, eaveV, topHu + Math.Abs(du), topHw + Math.Abs(dw), topV, wallHu + Math.Abs(du), wallHw + Math.Abs(dw), wallTopV, tile, valH);
            x.Kf = kf;
            x.K = k;
            return r;
        }

        /// <summary>How far an eave edge on side s (rectangle hu × hw, the side's length taken from the full eave
        /// <paramref name="along"/>) must rise to clear the corridors (0 when it does).</summary>
        private static double SideLift(KdsCtx x, int s, double hu, double hw, double edgeV, double along)
        {
            if (x.Clear == null) return 0;
            double need = 0;
            for (int q = 0; q <= 10; q++)
            {
                double u, w, px, py, pz;
                double a = -1 + q / 5.0;
                SacredParts.OnRect(s, a, s % 2 == 0 ? along + 0.05 : hu + 0.05, s % 2 == 0 ? hw + 0.05 : along + 0.05, out u, out w);
                x.World(u, 0, w, out px, out py, out pz);
                double min = x.Clear.MinHangY(px, pz, x.F.GroundY);
                if (double.IsNegativeInfinity(min)) continue;
                need = Math.Max(need, min - (x.F.GroundY + edgeV));
            }
            return need;
        }

        /// <summary>
        /// Fits an eave to the road corridors (contract §1): where a corridor runs close, the overhang is first pulled back
        /// toward the wall (in 15 % steps) until the edge clears the overhead clearance; when even a short overhang would
        /// hang into a corridor (the wall itself stands at one), the full overhang is kept and the returned lift (m) raises
        /// the tier as a whole. Returns 0 when the (possibly shortened) eave clears.
        /// </summary>
        public static double FitEave(KdsCtx x, ref double eaveHu, ref double eaveHw, double wallHu, double wallHw, double edgeV)
        {
            double lift = EaveLift(x, eaveHu, eaveHw, edgeV);
            if (lift <= 0) return 0;
            double ou = Math.Max(0, eaveHu - wallHu), ow = Math.Max(0, eaveHw - wallHw);
            for (int i = 1; i <= 6; i++)
            {
                double t = 1 - 0.15 * i, eu = wallHu + ou * t, ew = wallHw + ow * t;
                if (EaveLift(x, eu, ew, edgeV) > 0) continue;
                eaveHu = eu;
                eaveHw = ew;
                return 0;
            }
            return lift;
        }

        /// <summary>How far (m) an eave edge whose lowest point is at <paramref name="edgeV"/> must rise to clear every road
        /// corridor it overhangs by the overhead clearance (0 when it does).</summary>
        public static double EaveLift(KdsCtx x, double hu, double hw, double edgeV)
        {
            if (x.Clear == null) return 0;
            double need = 0;
            for (int s = 0; s < 4; s++)
            {
                for (int q = 0; q <= 10; q++)
                {
                    double u, w, px, py, pz;
                    SacredParts.OnRect(s, -1 + q / 5.0, hu + 0.05, hw + 0.05, out u, out w);
                    x.World(u, 0, w, out px, out py, out pz);
                    double min = x.Clear.MinHangY(px, pz, x.F.GroundY);
                    if (double.IsNegativeInfinity(min)) continue;
                    need = Math.Max(need, min - (x.F.GroundY + edgeV));
                }
            }
            return need;
        }

        /// <summary>
        /// The crimson pleated cloth valance hung under a roof's fascia (Kathmandu Durbar Square temples): a pale top band,
        /// pleats alternating crimson and pink, a scalloped lower edge; over a road corridor it stops at the overhead
        /// clearance (contract §1). LOD0 pleats every 0.32 m, LOD1 every 0.9 m (one colour), LOD2 a plain strip.
        /// </summary>
        public static void Valance(KdsCtx x, in RoofSpec r, double drop)
        {
            MeshData m = x.M;
            Affine3 k = x.K;
            int lod = x.Lod;
            double pitch = lod == 0 ? 0.8 : lod == 1 ? 1.5 : 3.0;
            for (int s = 0; s < 4; s++)
            {
                double len = 2 * (s % 2 == 0 ? r.EaveU : r.EaveW);
                int n = Math.Max(2, (int)Math.Round(len / pitch));
                double ou, ow;
                SacredParts.Out(s, out ou, out ow);
                for (int c = 0; c < n; c++)
                {
                    double a0 = -1 + 2.0 * c / n, a1 = -1 + 2.0 * (c + 1) / n;
                    ShapeBrush b = lod == 0 && c % 2 == 1 ? KdsLooks.ValanceFold : KdsLooks.Valance;
                    double u0, v0, w0, u1, v1, w1;
                    SacredParts.RoofPoint(r, s, a0, 0, out u0, out v0, out w0);
                    SacredParts.RoofPoint(r, s, a1, 0, out u1, out v1, out w1);
                    v0 -= r.FasciaH;
                    v1 -= r.FasciaH;
                    double off = 0.03 + (lod == 0 && c % 2 == 1 ? 0.025 : 0);
                    u0 += off * ou;
                    w0 += off * ow;
                    u1 += off * ou;
                    w1 += off * ow;
                    double d0 = drop, d1 = drop;
                    if (lod == 0)
                    {
                        // Scallops: the pleat hangs lower in its middle.
                        d0 = drop * 0.82;
                        d1 = drop * 0.82;
                    }
                    if (r.Clear != null)
                    {
                        d0 = ClampDrop(x, r, u0, v0, w0, d0);
                        d1 = ClampDrop(x, r, u1, v1, w1, d1);
                    }
                    if (d0 < 0.02 && d1 < 0.02) continue;
                    double band = lod == 0 ? Math.Min(0.07, 0.2 * drop) : 0;
                    if (band > 0)
                    {
                        SacredDraw.Quad(m, k, u0, v0, w0, u1, v1, w1, u1, v1 - band, w1, u0, v0 - band, w0, ou, -0.05, ow, KdsLooks.ValanceBand);
                        v0 -= band;
                        v1 -= band;
                        d0 -= band;
                        d1 -= band;
                    }
                    if (lod == 0)
                    {
                        double um = 0.5 * (u0 + u1), vm = 0.5 * (v0 + v1), wm = 0.5 * (w0 + w1), dm = Math.Min(drop - band, Math.Max(d0, d1) * 1.22);
                        if (r.Clear != null) dm = Math.Min(dm, ClampDrop(x, r, um, vm, wm, dm));
                        int a = SacredDraw.V(m, k, u0, v0, w0, ou, -0.05, ow, b), bb = SacredDraw.V(m, k, u1, v1, w1, ou, -0.05, ow, b);
                        int cm = SacredDraw.V(m, k, um, vm, wm, ou, -0.05, ow, b);
                        int c1 = SacredDraw.V(m, k, u1, v1 - d1, w1, ou, -0.05, ow, Looks.Ao(b, 0.85f));
                        int cmd = SacredDraw.V(m, k, um, vm - dm, wm, ou, -0.05, ow, Looks.Ao(b, 0.85f));
                        int c0 = SacredDraw.V(m, k, u0, v0 - d0, w0, ou, -0.05, ow, Looks.Ao(b, 0.85f));
                        SacredDraw.Quad(m, a, cm, cmd, c0);
                        SacredDraw.Quad(m, cm, bb, c1, cmd);
                    }
                    else
                    {
                        SacredDraw.Quad(m, k, u0, v0, w0, u1, v1, w1, u1, v1 - d1, w1, u0, v0 - d0, w0, ou, -0.05, ow, b);
                    }
                }
            }
        }

        private static double ClampDrop(KdsCtx x, in RoofSpec r, double u, double v, double w, double drop)
        {
            double px, py, pz;
            x.K.Point(u, v, w, out px, out py, out pz);
            double minY = r.Clear.MinHangY(px, pz, r.GroundY);
            return Math.Max(0, Math.Min(drop, py - minY));
        }

        /// <summary>The struts of a tier (perU / perW along the sides, winged corner struts at LOD0).</summary>
        public static int Struts(KdsCtx x, in RoofSpec r, double footV, int perU, int perW, bool rich, uint seed)
        {
            return SacredParts.Struts(x.M, x.K, r, footV, perU, perW, rich, KdsLooks.Strut, seed, x.Lod);
        }

        /// <summary>Eave bells every <paramref name="spacing"/> m (LOD0 only).</summary>
        public static int Bells(KdsCtx x, in RoofSpec r, double spacing, double size)
        {
            // Small eaves (under 3 m half) carry no bells: they are lost at that size and cost as much as the roof.
            if (Math.Max(r.EaveU, r.EaveW) < 3.0) return 0;
            return SacredParts.Bells(x.M, x.K, r, 1.5 * spacing, size, true, x.Lod);
        }

        /// <summary>
        /// An octagonal (any regular polygon) roof tier: eave ring of apothem <paramref name="eaveA"/> at eaveV, top ring of
        /// apothem topA at topV, soffit back to the wall ring (apothem wallA at wallTopV), jhingati courses at LOD0, the
        /// fascia, hip rolls on the corners with upturned horns and the crimson valance. The first side faces +w
        /// (<paramref name="rot"/> turns the polygon).
        /// </summary>
        public static void PolyRoof(KdsCtx x, int sides, double eaveA, double eaveV, double topA, double topV, double wallA, double wallTopV, in ShapeBrush tile,
                                    double valH, double rot = 0)
        {
            MeshData m = x.M;
            Affine3 k = x.K;
            int lod = x.Lod;
            double half = Math.PI / sides, toCorner = 1.0 / Math.Cos(half);
            int courses = lod == 0 ? Math.Max(2, (int)Math.Round(Math.Sqrt((eaveA - topA) * (eaveA - topA) + (topV - eaveV) * (topV - eaveV)) / 0.9)) : lod == 1 ? 3 : 1;
            int cols = lod == 0 ? 5 : lod == 1 ? 3 : 1;
            double fascia = Math.Min(0.2, 0.08 + 0.012 * eaveA), lift = 0.05 * eaveA, sag = 0.02 * eaveA;
            ShapeBrush ridge = Looks.Of(MeshColor.Scale(tile.Color, 0.7f), MaterialChannel.RoofTile);
            for (int s = 0; s < sides; s++)
            {
                // Side s: outward direction at angle phi (0 = +w), corners at phi ± half.
                double phi = rot + 2 * Math.PI * s / sides;
                double ca = phi - half, cb = phi + half;
                int first = m.VertexCount, rows = 0;
                bool[] seam = new bool[2 * courses + 2];
                for (int q = 0; q < courses; q++)
                {
                    double ta = (double)q / courses, tb = (double)(q + 1) / courses;
                    PolyRow(m, k, ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, cols, ta, lod == 0 ? 0.05 : 0, tile, ref rows);
                    PolyRow(m, k, ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, cols, tb, 0, tile, ref rows);
                    if (q + 1 < courses) seam[rows - 1] = true;
                }
                SacredDraw.Grid(m, first, rows, cols + 1, false, seam);
                // Fascia and soffit.
                double ou = Math.Sin(phi), ow = Math.Cos(phi);
                int ff = m.VertexCount;
                for (int row = 0; row < 3; row++)
                {
                    for (int c = 0; c <= cols; c++)
                    {
                        double a = -1 + 2.0 * c / cols, u, v, w;
                        PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, a, 0, out u, out v, out w);
                        if (row == 2)
                        {
                            // Back to the wall ring at its top.
                            double wu, ww;
                            PolyOnSide(ca, cb, wallA * toCorner, a, out wu, out ww);
                            SacredDraw.V(m, k, wu, wallTopV, ww, 0, -1, 0, Looks.Ao(KdsLooks.Timber, 0.5f));
                        }
                        else
                        {
                            SacredDraw.V(m, k, u + 0.01 * ou, v - row * fascia, w + 0.01 * ow, ou, row == 0 ? 0 : -0.3, ow, row == 0 ? KdsLooks.Timber : Looks.Ao(KdsLooks.Timber, 0.8f));
                        }
                    }
                }
                SacredDraw.Grid(m, ff, 2, cols + 1, false, null);
                // Soffit (between the fascia bottom row and the wall row): its own normals face down.
                int sf = m.VertexCount;
                for (int row = 0; row < 2; row++)
                {
                    for (int c = 0; c <= cols; c++)
                    {
                        double a = -1 + 2.0 * c / cols, u, v, w;
                        if (row == 0)
                        {
                            PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, a, 0, out u, out v, out w);
                            v -= fascia;
                        }
                        else
                        {
                            PolyOnSide(ca, cb, wallA * toCorner, a, out u, out w);
                            v = wallTopV;
                        }
                        SacredDraw.V(m, k, u, v, w, 0, -1, 0, Looks.Ao(KdsLooks.Timber, 0.5f));
                    }
                }
                SacredDraw.Grid(m, sf, 2, cols + 1, false, null);
                // Hip roll on the corner at cb.
                if (lod <= 1)
                {
                    var path = new Path3(4);
                    for (int q = 0; q <= 2; q++)
                    {
                        double t = q / 2.0, u, v, w;
                        PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, 1, t, out u, out v, out w);
                        path.Add(u, v + 0.07, w);
                    }
                    Shapes.Tube(m, k, ridge, path, 0.07, lod == 0 ? 5 : 3, true, false, default, 0.05);
                }
                // Valance under this side.
                if (valH > 0.01 && lod <= 2)
                {
                    int vn = lod == 0 ? Math.Max(3, (int)Math.Round(2 * eaveA * Math.Tan(half) / 0.32)) : lod == 1 ? 2 : 1;
                    for (int c = 0; c < vn; c++)
                    {
                        double a0 = -1 + 2.0 * c / vn, a1 = -1 + 2.0 * (c + 1) / vn, u0, v0, w0, u1, v1, w1;
                        PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, a0, 0, out u0, out v0, out w0);
                        PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, a1, 0, out u1, out v1, out w1);
                        double off = 0.03 + (lod == 0 && c % 2 == 1 ? 0.02 : 0);
                        ShapeBrush b = lod == 0 && c % 2 == 1 ? KdsLooks.ValanceFold : KdsLooks.Valance;
                        SacredDraw.Quad(m, k, u0 + off * ou, v0 - fascia, w0 + off * ow, u1 + off * ou, v1 - fascia, w1 + off * ow, u1 + off * ou, v1 - fascia - valH, w1 + off * ow,
                                        u0 + off * ou, v0 - fascia - valH, w0 + off * ow, ou, -0.05, ow, b);
                    }
                }
            }
            if (x.C != null) x.Cylinder(0, 0, eaveV, topV, wallA, false);
        }

        private static void PolyOnSide(double ca, double cb, double rCorner, double a, out double u, out double w)
        {
            double t = 0.5 * (a + 1);
            double au = rCorner * Math.Sin(ca), aw = rCorner * Math.Cos(ca), bu = rCorner * Math.Sin(cb), bw = rCorner * Math.Cos(cb);
            u = au + (bu - au) * t;
            w = aw + (bw - aw) * t;
        }

        private static void PolyPoint(double ca, double cb, double eaveA, double eaveV, double topA, double topV, double toCorner, double lift, double sag, double a,
                                      double t, out double u, out double v, out double w)
        {
            double ap = eaveA + (topA - eaveA) * t;
            PolyOnSide(ca, cb, ap * toCorner, a, out u, out w);
            double aa = Math.Abs(a), f = aa > 0.5 ? (aa - 0.5) / 0.5 : 0;
            v = eaveV + (topV - eaveV) * t + lift * f * f * (1 - t) * (1 - t) - sag * Math.Sin(Math.PI * t);
        }

        private static void PolyRow(MeshData m, in Affine3 k, double ca, double cb, double eaveA, double eaveV, double topA, double topV, double toCorner, double lift,
                                    double sag, int cols, double t, double off, in ShapeBrush b, ref int rows)
        {
            double phi = 0.5 * (ca + cb), slope = Math.Atan2(topV - eaveV, eaveA - topA);
            double nu = Math.Sin(phi) * Math.Sin(slope), nv = Math.Cos(slope), nw = Math.Cos(phi) * Math.Sin(slope);
            for (int c = 0; c <= cols; c++)
            {
                double a = -1 + 2.0 * c / cols, u, v, w;
                PolyPoint(ca, cb, eaveA, eaveV, topA, topV, toCorner, lift, sag, a, t, out u, out v, out w);
                SacredDraw.V(m, k, u + nu * off, v + nv * off, w + nw * off, nu, nv, nw, off > 0 ? Looks.Ao(b, 0.8f) : b);
            }
            rows++;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Finials
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The gilt gajur (kalasha, lotus and bell rings, parasol, point) of height h from v0; returns the tip.</summary>
        public static double GiltFinial(KdsCtx x, double v0, double h, double r)
        {
            // Finials under 2.4 m (all but the great temples') are drawn one LOD coarser: against the sky only their
            // stacked silhouette reads.
            return SacredParts.Gajur(x.M, x.K, v0, h, r, KdsLooks.Gilt, h < 2.4 ? Math.Min(2, x.Lod + 1) : x.Lod);
        }

        /// <summary>Maju Dega's finial: a white plastered bell-shaped dome on a square white base with a small gilt tip.</summary>
        public static double BellDomeFinial(KdsCtx x, double v0, double h, double r)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double bh = 0.18 * h;
            SacredDraw.Box(m, x.K, -1.05 * r, 1.05 * r, v0, v0 + bh, -1.05 * r, 1.05 * r, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
            var p = new Profile2(16);
            double dh = 0.62 * h;
            p.Add(0, 0, true).Add(0.95 * r, 0, true).Add(0.95 * r, 0.06 * dh).Add(0.82 * r, 0.12 * dh).Add(0.9 * r, 0.35 * dh).Add(0.78 * r, 0.62 * dh)
             .Add(0.5 * r, 0.85 * dh).Add(0.22 * r, 0.97 * dh).Add(0, dh);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, v0 + bh, 0), KdsLooks.Plaster, p, Seg(14, lod));
            double tip0 = v0 + bh + dh * 0.95, tipH = h - bh - dh * 0.95;
            var q = new Profile2(10);
            q.Add(0, 0, true).Add(0.2 * r, 0, true).Add(0.26 * r, 0.25 * tipH).Add(0.12 * r, 0.5 * tipH).Add(0.18 * r, 0.6 * tipH).Add(0.04 * r, 0.9 * tipH).Add(0, tipH);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, tip0, 0), KdsLooks.Gilt, q, Seg(8, lod));
            return v0 + h;
        }

        /// <summary>Kasthamandap's dark bronze finial: stacked rings and a bell over a square base, a tall spire with a small
        /// gilt tip.</summary>
        public static double BronzeFinial(KdsCtx x, double v0, double h, double r)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush b = KdsLooks.BronzeDark;
            var p = new Profile2(20);
            p.Add(0, 0, true).Add(1.1 * r, 0, true).Add(1.1 * r, 0.06 * h, true).Add(0.75 * r, 0.1 * h).Add(0.95 * r, 0.18 * h).Add(0.9 * r, 0.28 * h)
             .Add(0.55 * r, 0.36 * h).Add(0.7 * r, 0.42 * h, true).Add(0.4 * r, 0.47 * h).Add(0.55 * r, 0.52 * h, true).Add(0.3 * r, 0.57 * h).Add(0.24 * r, 0.72 * h)
             .Add(0.12 * r, 0.88 * h).Add(0, 0.95 * h);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, v0, 0), b, p, Seg(12, lod));
            Shapes.Cone(m, SacredDraw.At(x.K, 0, v0 + 0.93 * h, 0), KdsLooks.Gilt, 0.07 * r + 0.03, 0.07 * h, Seg(6, lod), 0, 0, false);
            return v0 + h;
        }

        /// <summary>Trailokya Mohan's white stepped pinnacle: receding white rings like a small stupa spire, a gilt tip.</summary>
        public static double WhitePinnacle(KdsCtx x, double v0, double h, double r)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            var p = new Profile2(24);
            p.Add(0, 0, true).Add(r, 0, true);
            int steps = lod == 0 ? 6 : 3;
            double y = 0, rr = r, body = 0.82 * h;
            for (int i = 0; i < steps; i++)
            {
                double hh = body / steps;
                p.Add(rr, y + 0.55 * hh, true).Add(rr * 0.82, y + 0.6 * hh, true);
                y += hh;
                rr *= 0.8;
                p.Add(rr * 1.02, y, true);
            }
            p.Add(0, y, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, v0, 0), KdsLooks.Plaster, p, Seg(12, lod));
            var q = new Profile2(8);
            double tip = h - y;
            q.Add(0, 0, true).Add(0.25 * r, 0, true).Add(0.3 * r, 0.3 * tip).Add(0.08 * r, 0.75 * tip).Add(0, tip);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, v0 + y, 0), KdsLooks.Gilt, q, Seg(8, lod));
            return v0 + h;
        }

        /// <summary>The ribbed amalaka cap of a shikhara spire whose top half size is <paramref name="topHalf"/> at v0;
        /// returns its top.</summary>
        public static double Amalaka(KdsCtx x, double topHalf, double v0, in ShapeBrush b)
        {
            double r = 1.3 * topHalf, h = 0.6 * topHalf;
            var p = new Profile2(12);
            p.Add(0, -0.05, true).Add(0.85 * topHalf, -0.05, true).Add(0.8 * topHalf, 0.08).Add(r, 0.35 * h).Add(r * 1.03, 0.55 * h).Add(r, 0.75 * h)
             .Add(0.6 * topHalf, h, true).Add(0.4 * topHalf, h + 0.05).Add(0, h + 0.05, true);
            Shapes.Lathe(x.M, SacredDraw.At(x.K, 0, v0, 0), Looks.Of(MeshColor.Scale(b.Color, 0.96f), b.Channel), p, Seg(16, x.Lod));
            return v0 + h + 0.05;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Figures and furniture
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A stone lion of height h on a moulded pedestal (hu × hw half, ph high) at (u, w), facing +w.</summary>
        public static void Lion(KdsCtx x, double u, double w, double ph, double h, int side, bool painted = false, double pedHalf = 0.32,
                                int quality = SacredFigures.Generic)
        {
            if (ph > 0.01) SacredParts.Pedestal(x.M, x.K, u, 0, w, pedHalf, pedHalf * 1.25, ph, KdsLooks.StonePale, x.Lod);
            // Beyond the nearest LOD the lions are a metre or two of figure seen from far off: drawn one LOD coarser.
            SacredFigures.Guardian(x.M, x.K, u, ph, w, h, GuardianKind.Lion, painted, quality, x.Lod == 0 ? 0 : Math.Min(3, x.Lod + 1), side);
            x.Solid(u - pedHalf, u + pedHalf, w - pedHalf * 1.25, w + pedHalf * 1.25, 0, ph + h, GenColliders.Stone);
        }

        /// <summary>A stone lion on its pedestal (see <see cref="Lion"/>) standing on a plinth stage of height
        /// <paramref name="baseV"/> instead of the ground.</summary>
        public static void LionOn(KdsCtx x, double u, double w, double baseV, double ph, double h, int side, bool painted = false, double pedHalf = 0.32,
                                  int quality = SacredFigures.Generic)
        {
            Affine3 k = x.K;
            KitFrame kf = x.Kf;
            GenFrame f = x.F;
            x.K = SacredDraw.At(k, 0, baseV, 0);
            x.Kf = kf.Offset(0, baseV, 0);
            x.F = new GenFrame(f.X, f.Z, (float)(f.GroundY + baseV), f.YawDeg);
            Lion(x, u, w, ph, h, side, painted, pedHalf, quality);
            x.K = k;
            x.Kf = kf;
            x.F = f;
        }

        /// <summary>A low wooden picket fence along a frame polyline segment (pointed stakes every ~0.17 m between posts
        /// every 2 m, two rails), skipped where it would stand in a road corridor; a fence collider.</summary>
        public static void Picket(KdsCtx x, double u0, double w0, double u1, double w1, double h)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double du = u1 - u0, dw = w1 - w0, len = Math.Sqrt(du * du + dw * dw);
            if (len < 0.3) return;
            double tu = du / len, tw = dw / len, nu = tw, nw = -tu;
            // Clear of the road corridors: trim both ends inward while inside one.
            double a = 0, b = len;
            while (a < b && x.CorridorDistance(u0 + tu * a, w0 + tw * a) < SacredClearance.MarginM) a += 0.25;
            while (b > a && x.CorridorDistance(u0 + tu * b, w0 + tw * b) < SacredClearance.MarginM) b -= 0.25;
            if (b - a < 0.4) return;
            double su = u0 + tu * a, sw = w0 + tw * a, l = b - a;
            ShapeBrush wood = KdsLooks.Fence;
            if (lod >= 2)
            {
                SacredDraw.Quad(m, x.K, su, 0, sw, su + tu * l, 0, sw + tw * l, su + tu * l, h, sw + tw * l, su, h, sw, nu, 0, nw, wood);
                SacredDraw.Quad(m, x.K, su, 0, sw, su + tu * l, 0, sw + tw * l, su + tu * l, h, sw + tw * l, su, h, sw, -nu, 0, -nw, wood);
            }
            else
            {
                Affine3 seg = SacredDraw.Basis(x.K, su, 0, sw, tu, 0, tw, 0, 1, 0);
                // Rails.
                BoxFaces rf = lod == 0 ? BoxFaces.All : BoxFaces.Front | BoxFaces.Back | BoxFaces.Top;
                SacredDraw.Box(m, seg, 0, l, 0.22, 0.3, -0.03, 0.03, Looks.Ao(wood, 0.85f), rf);
                SacredDraw.Box(m, seg, 0, l, 0.68, 0.76, -0.03, 0.03, Looks.Ao(wood, 0.85f), rf);
                // Posts.
                int posts = Math.Max(1, (int)Math.Round(l / 2.0));
                for (int i = 0; i <= posts; i++)
                {
                    double p = l * i / posts;
                    SacredDraw.Box(m, seg, p - 0.05, p + 0.05, 0, h + 0.08, -0.05, 0.05, wood, BoxFaces.All & ~BoxFaces.Bottom);
                }
                // Stakes: pointed boards (LOD0), plain wider boards (LOD1).
                double pitch = lod == 0 ? 0.4 : 0.6;
                int n = (int)(l / pitch);
                for (int i = 0; i < n; i++)
                {
                    double c = (i + 0.5) * l / n, hw = lod == 0 ? 0.035 : 0.09;
                    if (lod == 0)
                    {
                        SacredDraw.Box(m, seg, c - hw, c + hw, 0.05, h - 0.1, 0.03, 0.055, wood, BoxFaces.Front | BoxFaces.Back);
                        SacredDraw.Tri(m, seg, c - hw, h - 0.1, 0.055, c + hw, h - 0.1, 0.055, c, h, 0.055, 0, 0, 1, wood);
                        SacredDraw.Tri(m, seg, c - hw, h - 0.1, 0.03, c + hw, h - 0.1, 0.03, c, h, 0.03, 0, 0, -1, wood);
                    }
                    else
                    {
                        SacredDraw.Box(m, seg, c - hw, c + hw, 0.05, h, 0.03, 0.055, wood, BoxFaces.Front | BoxFaces.Back);
                    }
                }
            }
            x.Wall(su, sw, su + tu * l, sw + tw * l, 0, h, 0.05);
        }

        /// <summary>A low wire-mesh fence on thin posts (the Narayan ruin): a translucent-looking grey mesh panel band.</summary>
        public static void WireFence(KdsCtx x, double u0, double w0, double u1, double w1, double h)
        {
            MeshData m = x.M;
            double du = u1 - u0, dw = w1 - w0, len = Math.Sqrt(du * du + dw * dw);
            if (len < 0.3) return;
            double tu = du / len, tw = dw / len;
            double a = 0, b = len;
            while (a < b && x.CorridorDistance(u0 + tu * a, w0 + tw * a) < SacredClearance.MarginM) a += 0.25;
            while (b > a && x.CorridorDistance(u0 + tu * b, w0 + tw * b) < SacredClearance.MarginM) b -= 0.25;
            if (b - a < 0.4) return;
            double su = u0 + tu * a, sw = w0 + tw * a, l = b - a;
            Affine3 seg = SacredDraw.Basis(x.K, su, 0, sw, tu, 0, tw, 0, 1, 0);
            ShapeBrush wire = Looks.Of(MeshColor.FromHex(0x9A9C98), MaterialChannel.Metal, 0.9f);
            int posts = Math.Max(1, (int)Math.Round(l / 2.2));
            for (int i = 0; i <= posts; i++)
            {
                double p = l * i / posts;
                if (x.Lod <= 1) Shapes.Cylinder(m, SacredDraw.At(seg, p, 0, 0), KdsLooks.MetalGrey, 0.025, h + 0.05, 5, 0, 0, false, true);
            }
            // The mesh: two rails and a grid of thin wires at LOD0; a single dark band beyond.
            if (x.Lod == 0)
            {
                for (int r = 0; r <= 4; r++)
                {
                    double v = 0.1 + (h - 0.1) * r / 4;
                    SacredDraw.Box(m, seg, 0, l, v - 0.008, v + 0.008, -0.008, 0.008, wire, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
                }
                int n = (int)(l / 0.25);
                for (int i = 0; i <= n; i++)
                {
                    double c = l * i / Math.Max(1, n);
                    SacredDraw.Box(m, seg, c - 0.006, c + 0.006, 0.1, h, -0.006, 0.006, wire, BoxFaces.Front | BoxFaces.Back);
                }
            }
            else
            {
                SacredDraw.Box(m, seg, 0, l, 0.1, h, -0.005, 0.005, Looks.Of(MeshColor.FromHex(0x7A7C78), MaterialChannel.Metal, 0.8f), BoxFaces.Front | BoxFaces.Back);
            }
            x.Wall(su, sw, su + tu * l, sw + tw * l, 0, h, 0.03);
        }

        /// <summary>Horizontal tier mouldings round a curvilinear shikhara spire drawn by the shared spire generator (half
        /// size <paramref name="half"/> at its foot v0, height h): thin shaded rings following its Nagara curve, so the
        /// stacked courses of the white shikharas read (photos c_shivaparvati 14, c_kageshwor 00, KAT1910 10).</summary>
        public static void SpireBands(KdsCtx x, double half, double v0, double h, int bands, in ShapeBrush b)
        {
            if (x.Lod >= 2) return;
            if (x.Lod == 1) bands = Math.Max(1, bands / 2);
            for (int i = 1; i <= bands; i++)
            {
                double t = 0.85 * i / (bands + 1.0), hh = 1.08 * half * (1 - 0.7 * Math.Pow(t, 2.5)) + 0.02, v = v0 + h * t;
                SacredDraw.RectRing(x.M, x.K, 0, 0, hh, hh, SacredDraw.M.Add(0, v - 0.04, b).Add(0, v + 0.04), true, b);
            }
        }

        /// <summary>The gilt banner (pataka) hung from the finial down the front (+w) of a two-roof temple: a narrow gilt
        /// strip lying on the upper roof from just under the finial to its eave, dropping to the lower roof, down that to its
        /// eave and hanging free in front of the door below (Mahendreshwar, DANAM KAT1510 07).</summary>
        public static void Pataka(KdsCtx x, in RoofSpec upper, in RoofSpec lower, double hang, double halfW)
        {
            if (x.Lod >= 2) return;
            var pv = new double[16];
            var pw = new double[16];
            int n = 0;
            for (int i = 0; i <= 3; i++)
            {
                double u, v, w;
                SacredParts.RoofPoint(upper, 0, 0, 0.92 - 0.92 * i / 3.0, out u, out v, out w);
                pv[n] = v + 0.04;
                pw[n++] = w + 0.02;
            }
            double wd = upper.EaveW + 0.06;
            double tl = Math.Max(0, Math.Min(1, (lower.EaveW - wd) / Math.Max(0.01, lower.EaveW - lower.TopW)));
            for (int i = 0; i <= 3; i++)
            {
                double u, v, w;
                SacredParts.RoofPoint(lower, 0, 0, tl * (1 - i / 3.0), out u, out v, out w);
                pv[n] = v + 0.04;
                pw[n++] = i == 0 ? wd : w + 0.02;
            }
            pv[n] = pv[n - 1] - lower.FasciaH - hang;
            pw[n] = pw[n - 1] + 0.02;
            n++;
            ShapeBrush gilt = KdsLooks.Gilt, back = Looks.Ao(KdsLooks.Gilt, 0.6f);
            for (int i = 0; i + 1 < n; i++)
            {
                double dv = pv[i + 1] - pv[i], dw = pw[i + 1] - pw[i], l = Math.Sqrt(dv * dv + dw * dw);
                if (l < 1e-4) continue;
                // Normal in the (v, w) plane, facing up and out.
                double nv = dw / l, nw = -dv / l;
                if (nv < 0 || (Math.Abs(nv) < 1e-6 && nw < 0))
                {
                    nv = -nv;
                    nw = -nw;
                }
                SacredDraw.Quad(x.M, x.K, -halfW, pv[i], pw[i], halfW, pv[i], pw[i], halfW, pv[i + 1], pw[i + 1], -halfW, pv[i + 1], pw[i + 1], 0, nv, nw, gilt);
                SacredDraw.Quad(x.M, x.K, halfW, pv[i], pw[i], -halfW, pv[i], pw[i], -halfW, pv[i + 1], pw[i + 1], halfW, pv[i + 1], pw[i + 1], 0, -nv, -nw, back);
            }
        }

        /// <summary>A hanging temple bell on a small iron frame (at LOD0): two posts, a bar and the bell.</summary>
        public static void BellFrame(KdsCtx x, double u, double w, double v, double h, double bellD)
        {
            if (x.Lod >= 2) return;
            MeshData m = x.M;
            ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x3A3836), MaterialChannel.Metal);
            Shapes.Bar(m, x.K, iron, u - 0.4, v, w, u - 0.4, v + h, w, 0.03, 5);
            Shapes.Bar(m, x.K, iron, u + 0.4, v, w, u + 0.4, v + h, w, 0.03, 5);
            Shapes.Bar(m, x.K, iron, u - 0.45, v + h, w, u + 0.45, v + h, w, 0.035, 5);
            BigBell(x, u, v + h - 0.06, w, bellD);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Ornament
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Repaints the vertices from <paramref name="first"/> on in a brush, keeping their painted brightness as
        /// shading (gilt winged lions and brass lions made with the stone figure kit).</summary>
        public static void Recolour(MeshData m, int first, in ShapeBrush b, float floor = 0.45f)
        {
            int r = MeshColor.R(b.Color), g = MeshColor.G(b.Color), bl = MeshColor.B(b.Color);
            for (int v = first; v < m.VertexCount; v++)
            {
                int c = 4 * v;
                float lum = (0.3f * m.Colors[c] + 0.59f * m.Colors[c + 1] + 0.11f * m.Colors[c + 2]) / 236f;
                float f = Math.Max(floor, Math.Min(1.08f, lum));
                m.Colors[c] = (byte)Math.Min(255f, r * f);
                m.Colors[c + 1] = (byte)Math.Min(255f, g * f);
                m.Colors[c + 2] = (byte)Math.Min(255f, bl * f);
                if (m.HasUv0) m.Uv0[2 * v] = (float)b.Channel;
            }
        }

        /// <summary>
        /// A flat arch band on a wall plane (side frame sk, plane w = d, standing out by t): two legs from v0 and a round head
        /// of inner half width <paramref name="halfW"/> closing at v0 + h, the band <paramref name="band"/> wide; an ogee point
        /// at the crown with <paramref name="ogee"/>, flame tongues on the outer edge at LOD0 with <paramref name="flames"/>
        /// (the gilt flame arches, the Kal Bhairav niche rim, the prabhamandala of the pinnacles).
        /// </summary>
        public static void FlameArch(MeshData m, in Affine3 sk, double u, double v0, double d, double halfW, double h, double band, double t,
                                     in ShapeBrush b, int lod, bool flames, bool ogee)
        {
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int seg = lod == 0 ? 14 : lod == 1 ? 8 : 4;
            double ro = halfW + band, spring = v0 + Math.Max(0.05, h - halfW - (ogee ? 0.25 * halfW : 0));
            int n = 0;
            // Outer contour: left foot, up, over the head, down the right leg.
            px[n] = u - ro;
            pz[n++] = -v0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * (1 - (double)i / seg), r = ro;
                if (flames && lod == 0 && i % 2 == 1) r += 0.45 * band + 0.06;
                double pv = spring + r * Math.Sin(a);
                if (ogee && i == seg / 2) pv += 0.3 * halfW + band;
                px[n] = u + r * Math.Cos(a);
                pz[n++] = -pv;
            }
            px[n] = u + ro;
            pz[n++] = -v0;
            // Inner contour back.
            px[n] = u + halfW;
            pz[n++] = -v0;
            for (int i = seg; i >= 0; i--)
            {
                double a = Math.PI * (1 - (double)i / seg);
                double pv = spring + halfW * Math.Sin(a);
                if (ogee && i == seg / 2) pv += 0.25 * halfW;
                px[n] = u + halfW * Math.Cos(a);
                pz[n++] = -pv;
            }
            px[n] = u - halfW;
            pz[n++] = -v0;
            Affine3 f = SacredDraw.Basis(sk, 0, 0, d, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, f, b, px, pz, n, t, lod == 0 ? 0.015 : 0, lod == 0 ? 1 : 0, BevelStyle.Chamfer, true, false);
        }

        /// <summary>A flat filled arch panel (the field inside an arch) on a wall plane, the same outline as
        /// <see cref="FlameArch"/>'s inner edge.</summary>
        public static void ArchPanel(MeshData m, in Affine3 sk, double u, double v0, double d, double halfW, double h, double t, in ShapeBrush b, int lod,
                                     bool ogee)
        {
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int seg = lod == 0 ? 14 : lod == 1 ? 8 : 4;
            double spring = v0 + Math.Max(0.05, h - halfW - (ogee ? 0.25 * halfW : 0));
            int n = 0;
            px[n] = u - halfW;
            pz[n++] = -v0;
            px[n] = u + halfW;
            pz[n++] = -v0;
            for (int i = seg; i >= 0; i--)
            {
                double a = Math.PI * (1 - (double)i / seg);
                double pv = spring + halfW * Math.Sin(a);
                if (ogee && i == seg / 2) pv += 0.25 * halfW;
                px[n] = u + halfW * Math.Cos(a);
                pz[n++] = -pv;
            }
            Affine3 f = SacredDraw.Basis(sk, 0, 0, d, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, f, b, px, pz, n, t, 0, 0, BevelStyle.Chamfer, true, false);
        }

        /// <summary>A gilt gajur with a flat flame arch (prabhamandala) round it, standing on a ridge at (0, v0, 0) facing
        /// +w (Silyan Sattal's pinnacle); returns the top.</summary>
        public static double FlamePinnacle(KdsCtx x, double v0, double h)
        {
            double tip = GiltFinial(x, v0, 0.82 * h, 0.16 + 0.05 * h);
            if (x.Lod <= 1) FlameArch(x.M, x.K, 0, v0 + 0.1, -0.03, 0.32 * h, 0.9 * h, 0.07 + 0.03 * h, 0.06, KdsLooks.Gilt, x.Lod, true, true);
            return Math.Max(tip, v0 + h);
        }

        /// <summary>Plank shutters on a wall face (side frame sk, plane d, half length a, v0..v1): framed panels every
        /// <paramref name="bay"/> m with vertical boards (the closed timber ground floors of the rest houses).</summary>
        public static void Shutters(MeshData m, in Affine3 sk, double a, double d, double v0, double v1, double bay, int lod, in ShapeBrush wood)
        {
            if (lod >= 2) return;
            int bays = Math.Max(1, (int)Math.Round(2 * a / bay));
            ShapeBrush frame = Looks.Ao(wood, 0.8f), board = Looks.Of(MeshColor.Scale(wood.Color, 1.12f), wood.Channel);
            double fw = 0.07;
            for (int i = 0; i <= bays; i++)
            {
                double u = -a + 2 * a * i / bays;
                SacredDraw.Box(m, sk, u - fw, u + fw, v0, v1, d, d + 0.06, frame, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            SacredDraw.Box(m, sk, -a, a, v1 - 0.12, v1, d, d + 0.07, frame, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            SacredDraw.Box(m, sk, -a, a, v0, v0 + 0.15, d, d + 0.07, frame, BoxFaces.Front | BoxFaces.Top);
            if (lod >= 1) return;
            for (int i = 0; i < bays; i++)
            {
                double c0 = -a + 2 * a * i / bays + fw, c1 = -a + 2 * a * (i + 1) / bays - fw;
                // A mid rail and the board joints.
                SacredDraw.Box(m, sk, c0, c1, v0 + 0.45 * (v1 - v0), v0 + 0.45 * (v1 - v0) + 0.08, d, d + 0.04, frame, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                int boards = Math.Max(2, (int)((c1 - c0) / 0.22));
                for (int q = 1; q < boards; q++)
                {
                    double uu = c0 + (c1 - c0) * q / boards;
                    SacredDraw.Box(m, sk, uu - 0.012, uu + 0.012, v0 + 0.15, v1 - 0.12, d, d + 0.018, board, BoxFaces.Front);
                }
            }
        }

        /// <summary>A carved balcony rail between two frame points (top rail, bottom rail and balusters at LOD0), with a wall
        /// collider so nobody falls off a gallery.</summary>
        public static void Balustrade(KdsCtx x, double u0, double w0, double u1, double w1, double v0, double h, in ShapeBrush wood)
        {
            double du = u1 - u0, dw = w1 - w0, len = Math.Sqrt(du * du + dw * dw);
            if (len < 0.2) return;
            Affine3 seg = SacredDraw.Basis(x.K, u0, v0, w0, du / len, 0, dw / len, 0, 1, 0);
            ShapeBrush rail = Looks.Ao(wood, 0.9f);
            if (x.Lod >= 2)
            {
                SacredDraw.Box(x.M, seg, 0, len, 0, h, -0.03, 0.03, wood, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            }
            else
            {
                SacredDraw.Box(x.M, seg, 0, len, h - 0.1, h, -0.06, 0.06, rail, BoxFaces.All);
                SacredDraw.Box(x.M, seg, 0, len, 0, 0.12, -0.06, 0.06, rail, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
                int n = Math.Max(2, (int)(len / (x.Lod == 0 ? 0.28 : 0.5)));
                for (int i = 1; i < n; i++)
                {
                    double c = len * i / n;
                    SacredDraw.Box(x.M, seg, c - 0.03, c + 0.03, 0.12, h - 0.1, -0.03, 0.03, wood, x.Lod == 0 ? BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front | BoxFaces.Back);
                }
            }
            x.Wall(u0, w0, u1, w1, v0, v0 + h, 0.06);
        }

        /// <summary>A large bronze bell (mouth diameter d) hanging with its crown at vTop under a beam at (u, w): a thick
        /// lip, inscription bands and the dragon-loop crown (the Taleju Bell, the Kal Bhairav bells).</summary>
        public static void BigBell(KdsCtx x, double u, double vTop, double w, double d)
        {
            int lod = x.Lod;
            double r = 0.5 * d, bh = 0.95 * d;
            var p = new Profile2(20);
            p.Add(0, 0.04 * bh, true).Add(0.86 * r, 0.04 * bh).Add(r, 0, true).Add(1.02 * r, 0.05 * bh, true).Add(0.96 * r, 0.1 * bh);
            if (lod == 0) p.Add(0.9 * r, 0.2 * bh).Add(0.92 * r, 0.22 * bh, true).Add(0.88 * r, 0.24 * bh);
            p.Add(0.78 * r, 0.45 * bh);
            if (lod == 0) p.Add(0.76 * r, 0.6 * bh).Add(0.79 * r, 0.62 * bh, true).Add(0.75 * r, 0.64 * bh);
            p.Add(0.7 * r, 0.82 * bh).Add(0.55 * r, 0.95 * bh).Add(0.2 * r, bh).Add(0, bh, true);
            // Hand bells (under 0.6 m) read round with fewer sides than the great bells.
            Shapes.Lathe(x.M, SacredDraw.At(x.K, u, vTop - bh, w), KdsLooks.BellBronze, p, Seg(d < 0.6 ? 10 : 16, lod));
            if (lod <= 1)
            {
                // The crown loop and the clapper.
                Shapes.Torus(x.M, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x.K, u, vTop + 0.08, w)), KdsLooks.BronzeDark, 0.1 + 0.05 * d, 0.035,
                             8, 4);
                Shapes.Cylinder(x.M, SacredDraw.At(x.K, u, vTop - bh - 0.25, w), KdsLooks.BronzeDark, 0.03, bh * 0.8, 5);
                Shapes.Sphere(x.M, SacredDraw.At(x.K, u, vTop - bh - 0.2, w), KdsLooks.BronzeDark, 0.08 + 0.03 * d, 6);
            }
        }

        /// <summary>A stepped bell-shaped ribbed dome with a ring spire and a gilt tip (the three domes on Kal Bhairav's
        /// shrine): base half size b on v0, total height h; returns the tip.</summary>
        public static double RibbedDome(KdsCtx x, double u, double w, double v0, double h, double b, in ShapeBrush body)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            SacredDraw.Box(m, x.K, u - b, u + b, v0, v0 + 0.12 * h, w - b, w + b, body, BoxFaces.All & ~BoxFaces.Bottom);
            double dv = v0 + 0.12 * h, dh = 0.42 * h, r = 0.92 * b;
            var p = new Profile2(16);
            p.Add(0, 0, true).Add(r, 0, true).Add(r * 1.02, 0.08 * dh).Add(0.95 * r, 0.35 * dh).Add(0.8 * r, 0.62 * dh).Add(0.55 * r, 0.85 * dh).Add(0.28 * r, dh)
             .Add(0, dh, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, dv, w), body, p, Seg(16, lod));
            if (lod == 0)
            {
                // Vertical ribs.
                for (int i = 0; i < 8; i++)
                {
                    double a = 2 * Math.PI * i / 8, ca = Math.Cos(a), sa = Math.Sin(a);
                    var path = new Path3(4);
                    path.Add(u + 1.0 * r * ca, dv + 0.05 * dh, w + 1.0 * r * sa).Add(u + 0.97 * r * ca, dv + 0.38 * dh, w + 0.97 * r * sa)
                        .Add(u + 0.8 * r * ca, dv + 0.65 * dh, w + 0.8 * r * sa).Add(u + 0.5 * r * ca, dv + 0.88 * dh, w + 0.5 * r * sa);
                    Shapes.Tube(m, x.K, Looks.Ao(body, 0.85f), path, 0.035, 4, false);
                }
            }
            // The spire of stacked rings and the tip.
            double sv = dv + dh, sh = h - (sv - v0);
            var q = new Profile2(20);
            q.Add(0, 0, true).Add(0.32 * r, 0, true);
            int rings = lod == 0 ? 5 : 2;
            for (int i = 0; i < rings; i++)
            {
                double t0 = 0.62 * sh * i / rings, rr = 0.3 * r * (1 - 0.65 * i / rings);
                q.Add(rr, t0 + 0.03, true).Add(rr * 0.78, t0 + 0.6 * 0.62 * sh / rings, true);
            }
            q.Add(0.14 * r, 0.64 * sh, true).Add(0.2 * r, 0.7 * sh).Add(0, 0.72 * sh, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, sv, w), body, q, Seg(10, lod));
            var t = new Profile2(8);
            double th = 0.28 * sh;
            t.Add(0, 0, true).Add(0.07 * r + 0.02, 0, true).Add(0.09 * r + 0.02, 0.3 * th).Add(0.03, 0.8 * th).Add(0, th);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, sv + 0.72 * sh, w), KdsLooks.Gilt, t, Seg(8, lod));
            return v0 + h;
        }

        // -------------------------------------------------------------------------------------------------------------
        // LOD3
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The far stand-in (≤ 200 triangles): a plinth block, a body and stacked pyramid roofs at the given
        /// eave half widths and heights, closing at <paramref name="top"/>.</summary>
        public static void Lod3(KdsCtx x, double plinthHu, double plinthHw, double plinthTop, double bodyHu, double bodyHw, double[] eaveHalf, double[] eaveV,
                                double[] roofTop, double top, in ShapeBrush plinth, in ShapeBrush body, in ShapeBrush roof)
        {
            MeshData m = x.M;
            Affine3 k = x.K;
            if (plinthTop > 0.05) SacredDraw.Box(m, k, -plinthHu, plinthHu, -0.2, plinthTop, -plinthHw, plinthHw, plinth, BoxFaces.All & ~BoxFaces.Bottom);
            double v = plinthTop;
            for (int i = 0; i < eaveHalf.Length; i++)
            {
                double bh = i == 0 ? bodyHu : 0.5 * eaveHalf[i] * 0.9, bw = i == 0 ? bodyHw : bh * bodyHw / Math.Max(1e-6, bodyHu);
                SacredDraw.Box(m, k, -bh, bh, v, eaveV[i], -bw, bw, body, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                double eu = eaveHalf[i], ew = eaveHalf[i] * bodyHw / Math.Max(1e-6, bodyHu), tv = i + 1 < eaveHalf.Length ? roofTop[i] : top;
                double tu = i + 1 < eaveHalf.Length ? 0.45 * eaveHalf[i + 1] : 0.05, tw = tu * ew / Math.Max(1e-6, eu);
                for (int s = 0; s < 4; s++)
                {
                    double au, aw, bu, bw2, cu, cw, du, dw;
                    SacredParts.OnRect(s, -1, eu, ew, out au, out aw);
                    SacredParts.OnRect(s, 1, eu, ew, out bu, out bw2);
                    SacredParts.OnRect(s, 1, tu, tw, out cu, out cw);
                    SacredParts.OnRect(s, -1, tu, tw, out du, out dw);
                    double ou, ow;
                    SacredParts.Out(s, out ou, out ow);
                    SacredDraw.Quad(m, k, au, eaveV[i], aw, bu, eaveV[i], bw2, cu, tv, cw, du, tv, dw, ou, 1, ow, roof);
                }
                v = tv;
            }
        }
    }
}
