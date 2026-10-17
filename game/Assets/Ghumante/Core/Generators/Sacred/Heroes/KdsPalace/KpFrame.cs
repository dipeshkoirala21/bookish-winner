using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// Where the Hanuman Dhoka palace site stands in a tile (docs/research/heritage/kds-palace.md §2.1): the brief's local
    /// metres (x east, y north from 27.704500 N, 85.307500 E; v up from the square's one level) mapped to tile-local
    /// metres with absolute heights through the game frame's Jacobian at the origin (the game grid is turned about 0.6°
    /// from true north here, so a plain offset would put the far end of the site a metre off its OSM outline). The
    /// palace stands on a grid turned 12° clockwise (§2.1): grid a points to bearing 102°, grid b to 12°. Face frames
    /// follow the kit convention of the sacred generators (u across a facade, the viewer's right as they face it; v up;
    /// w out through it). Collider helpers take site coordinates and write tile-local colliders.
    /// </summary>
    internal struct KpFrame
    {
        /// <summary>cos and sin of the palace grid's 12° turn.</summary>
        public const double C12 = 0.97814760073380569, S12 = 0.20791169081775934;

        /// <summary>Tile-local position of the site origin.</summary>
        public double OX, OZ;

        /// <summary>Tile metres per local metre east (EX, EZ) and north (NX, NZ).</summary>
        public double EX, EZ, NX, NZ;

        /// <summary>Absolute height of the square (the flattened level, <see cref="KdsPalaceSite.LevelM"/>).</summary>
        public double Level;

        public int Lod;

        /// <summary>Colliders of LOD0 (null at the other LODs).</summary>
        public GenColliders C;

        public TileData Tile;

        public static KpFrame For(TileData tile, int lod, GenColliders c)
        {
            double gx, gz, ex, ez, nx, nz;
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon, KdsPalaceSite.OriginLat, out gx, out gz);
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon + 50.0 / KdsPalaceSite.MetresPerDegLon, KdsPalaceSite.OriginLat, out ex, out ez);
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon, KdsPalaceSite.OriginLat + 50.0 / KdsPalaceSite.MetresPerDegLat, out nx, out nz);
            var f = new KpFrame
            {
                OX = gx - tile.Tile.X0, OZ = gz - tile.Tile.Z0, EX = (ex - gx) / 50.0, EZ = (ez - gz) / 50.0, NX = (nx - gx) / 50.0, NZ = (nz - gz) / 50.0,
                Level = KdsPalaceSite.LevelM, Lod = Math.Max(0, Math.Min(3, lod)), C = lod == 0 ? c : null, Tile = tile,
            };
            return f;
        }

        public ShapeLod SL
        {
            get { return new ShapeLod(Math.Min(2, Lod)); }
        }

        /// <summary>Tile-local (x, z) of local (x, y).</summary>
        public void ToTile(double x, double y, out double tx, out double tz)
        {
            tx = OX + x * EX + y * NX;
            tz = OZ + x * EZ + y * NZ;
        }

        /// <summary>Local (x, y) of tile-local (tx, tz).</summary>
        public void FromTile(double tx, double tz, out double x, out double y)
        {
            double dx = tx - OX, dz = tz - OZ, det = EX * NZ - NX * EZ;
            x = (dx * NZ - dz * NX) / det;
            y = (-dx * EZ + dz * EX) / det;
        }

        /// <summary>Local (x, y) of palace-grid (a, b).</summary>
        public static void FromGrid(double a, double b, out double x, out double y)
        {
            x = a * C12 + b * S12;
            y = -a * S12 + b * C12;
        }

        /// <summary>Palace-grid (a, b) of local (x, y).</summary>
        public static void ToGrid(double x, double y, out double a, out double b)
        {
            a = x * C12 - y * S12;
            b = x * S12 + y * C12;
        }

        /// <summary>A tile vector of a local direction.</summary>
        private void Dir(double lx, double ly, out double tx, out double tz)
        {
            tx = lx * EX + ly * NX;
            tz = lx * EZ + ly * NZ;
        }

        /// <summary>A face frame at local (x, y), height v above the square, whose +w points to compass bearing
        /// <paramref name="bearingDeg"/> (u = the viewer's right as they face it, as <see cref="KitFrame.FromYaw"/>).</summary>
        public Affine3 Face(double x, double y, double v, double bearingDeg)
        {
            double r = bearingDeg * Math.PI / 180.0, s = Math.Sin(r), c = Math.Cos(r);
            double ux, uz, wx, wz, tx, tz;
            Dir(-c, s, out ux, out uz);
            Dir(s, c, out wx, out wz);
            ToTile(x, y, out tx, out tz);
            return Affine3.FromBasis(ux, 0, uz, 0, 1, 0, wx, 0, wz, tx, Level + v, tz);
        }

        /// <summary>A face frame at palace-grid (a, b) (see <see cref="Face"/>).</summary>
        public Affine3 GridFace(double a, double b, double v, double bearingDeg)
        {
            double x, y;
            FromGrid(a, b, out x, out y);
            return Face(x, y, v, bearingDeg);
        }

        /// <summary>A plan frame at local (cx, cy): its x axis points to bearing <paramref name="xBearingDeg"/>, its z
        /// axis to that bearing minus 90° (a building's own "east" and "north"), y up from the square.</summary>
        public Affine3 Plan(double cx, double cy, double v, double xBearingDeg)
        {
            double r = xBearingDeg * Math.PI / 180.0, s = Math.Sin(r), c = Math.Cos(r);
            double ax, az, bx, bz, tx, tz;
            Dir(s, c, out ax, out az);
            Dir(-c, s, out bx, out bz);
            ToTile(cx, cy, out tx, out tz);
            return Affine3.FromBasis(ax, 0, az, 0, 1, 0, bx, 0, bz, tx, Level + v, tz);
        }

        /// <summary>The palace grid as a plan frame: (a, v, b) to tile-local.</summary>
        public Affine3 Grid
        {
            get { return Plan(0, 0, 0, 102); }
        }

        /// <summary>The face frame of side <paramref name="side"/> of a grid rectangle (0 = +b, bearing 12°; 1 = +a, 102°;
        /// 2 = −b, 192°; 3 = −a, 282°) at the middle of that wall, from height v; <paramref name="half"/> is the half
        /// length of the side.</summary>
        public Affine3 GridSide(double a0, double a1, double b0, double b1, double v, int side, out double half)
        {
            double am = 0.5 * (a0 + a1), bm = 0.5 * (b0 + b1);
            switch (side & 3)
            {
                case 0:
                    half = 0.5 * (a1 - a0);
                    return GridFace(am, b1, v, 12);
                case 1:
                    half = 0.5 * (b1 - b0);
                    return GridFace(a1, bm, v, 102);
                case 2:
                    half = 0.5 * (a1 - a0);
                    return GridFace(am, b0, v, 192);
                default:
                    half = 0.5 * (b1 - b0);
                    return GridFace(a0, bm, v, 282);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Colliders (LOD0 only)
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A box collider centred at local (cx, cy), half sizes along bearing <paramref name="alongDeg"/> and across
        /// it, from v0 to v1 above the square.</summary>
        public void Box(double cx, double cy, double halfAlong, double halfAcross, double alongDeg, double v0, double v1, GenColliderFlags flags, byte material)
        {
            if (C == null || !(halfAlong > 0) || !(halfAcross > 0) || !(v1 > v0)) return;
            double r = alongDeg * Math.PI / 180.0, ux, uz, tx, tz;
            Dir(Math.Sin(r), Math.Cos(r), out ux, out uz);
            double l = Math.Sqrt(ux * ux + uz * uz);
            ToTile(cx, cy, out tx, out tz);
            C.AddBox(tx, tz, Level + v0, Level + v1, halfAlong * l, halfAcross * l, ux / l, uz / l, flags, material);
        }

        /// <summary>A box collider over a palace-grid rectangle.</summary>
        public void GridBox(double a0, double a1, double b0, double b1, double v0, double v1, GenColliderFlags flags, byte material)
        {
            double x, y;
            FromGrid(0.5 * (a0 + a1), 0.5 * (b0 + b1), out x, out y);
            Box(x, y, 0.5 * Math.Abs(a1 - a0), 0.5 * Math.Abs(b1 - b0), 102, v0, v1, flags, material);
        }

        /// <summary>A walkable ramp (a stair) from local (x0, y0, v0) to (x1, y1, v1).</summary>
        public void Ramp(double x0, double y0, double v0, double x1, double y1, double v1, double halfWidth, byte material)
        {
            if (C == null) return;
            double tx0, tz0, tx1, tz1;
            ToTile(x0, y0, out tx0, out tz0);
            ToTile(x1, y1, out tx1, out tz1);
            C.AddRamp(new GenRamp
            {
                X0 = tx0, Z0 = tz0, X1 = tx1, Z1 = tz1, Y0 = (float)(Level + v0), Y1 = (float)(Level + v1), HalfWidth = (float)halfWidth, Material = material,
            });
        }

        /// <summary>A ramp between two palace-grid points.</summary>
        public void GridRamp(double a0, double b0, double v0, double a1, double b1, double v1, double halfWidth, byte material)
        {
            double x0, y0, x1, y1;
            FromGrid(a0, b0, out x0, out y0);
            FromGrid(a1, b1, out x1, out y1);
            Ramp(x0, y0, v0, x1, y1, v1, halfWidth, material);
        }

        /// <summary>A solid vertical cylinder at local (x, y).</summary>
        public void Cylinder(double x, double y, double v0, double v1, double radius, bool cameraPasses = false)
        {
            if (C == null) return;
            double tx, tz;
            ToTile(x, y, out tx, out tz);
            C.AddCylinder(tx, tz, Level + v0, Level + v1, radius, cameraPasses);
        }

        /// <summary>A thin wall between two local points.</summary>
        public void Wall(double x0, double y0, double x1, double y1, double v0, double v1, double halfThickness)
        {
            if (C == null) return;
            double tx0, tz0, tx1, tz1;
            ToTile(x0, y0, out tx0, out tz0);
            ToTile(x1, y1, out tx1, out tz1);
            C.AddWall(tx0, tz0, tx1, tz1, Level + v0, Level + v1, halfThickness);
        }

        /// <summary>A thin wall between two palace-grid points.</summary>
        public void GridWall(double a0, double b0, double a1, double b1, double v0, double v1, double halfThickness)
        {
            double x0, y0, x1, y1;
            FromGrid(a0, b0, out x0, out y0);
            FromGrid(a1, b1, out x1, out y1);
            Wall(x0, y0, x1, y1, v0, v1, halfThickness);
        }
    }
}
