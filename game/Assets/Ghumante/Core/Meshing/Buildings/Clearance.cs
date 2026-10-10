using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Overhead clearance of facade projections (decision 2 of docs/W2_DETAIL_CONTRACT.md): nothing that projects from
    /// a wall (balcony, eave, strut, sign, awning, sunshade, cornice, apron) may reach into a road corridor below
    /// <see cref="RoadClearance.MinOverheadClearanceM"/> above the ground in front of it. <see cref="Depth"/> returns how
    /// far an element may project: the full depth when it is high enough or the corridor is far enough, else the free
    /// depth in front of the wall (zero at a wall on the corridor edge), so the builder clips it, raises it or leaves it
    /// out. Frames and positions are tile-local metres with absolute Y (as <see cref="KitFrame"/>); the corridor query
    /// is in game metres. A struct over shared immutable state: no allocation.
    /// </summary>
    internal struct Clearance
    {
        /// <summary>Clipped elements end this far short of the corridor.</summary>
        public const double MarginM = 0.02;

        private readonly IRoadCorridorQuery _q;
        private readonly double _x0, _z0;

        public Clearance(TileData t, IRoadCorridorQuery q)
        {
            _q = q;
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
        }

        /// <summary>False when there is no corridor query (every projection is allowed).</summary>
        public bool Active
        {
            get { return _q != null; }
        }

        /// <summary>Signed distance from frame point (u, w) at wall height to the nearest corridor edge (m, negative
        /// inside a corridor); +∞ without a query.</summary>
        public double Free(in KitFrame f, double u, double w)
        {
            if (_q == null) return double.PositiveInfinity;
            double x, y, z;
            f.ToWorld(u, 0, w, out x, out y, out z);
            return _q.SignedDistance(_x0 + x, _z0 + z);
        }

        /// <summary>Signed distance from tile-local (lx, lz) to the nearest corridor edge (negative inside); +∞ without
        /// a query.</summary>
        public double FreeAt(double lx, double lz)
        {
            return _q == null ? double.PositiveInfinity : _q.SignedDistance(_x0 + lx, _z0 + lz);
        }

        /// <summary>
        /// How far an element spanning [<paramref name="u0"/>, <paramref name="u1"/>] of a facade may project out of the
        /// wall plane w = <paramref name="w0"/> when its lowest point is at absolute height <paramref name="bottomY"/>:
        /// <paramref name="want"/> when it stays clear of every corridor or clears the overhead minimum above the ground
        /// in front of it, else the free depth (≥ 0) before the nearest corridor.
        /// </summary>
        public double Depth(in KitFrame f, double u0, double u1, double w0, double bottomY, double want, ref BuildingGround g)
        {
            if (_q == null || want <= 0) return want;
            double um = 0.5 * (u0 + u1);
            double free = Math.Min(Free(f, u0, w0), Math.Min(Free(f, u1, w0), Free(f, um, w0))) - MarginM;
            if (free >= want && Free(f, u0, w0 + want) >= 0 && Free(f, u1, w0 + want) >= 0 && Free(f, um, w0 + want) >= 0) return want;
            double x, y, z, ground = double.MinValue;
            f.ToWorld(u0, 0, w0 + want, out x, out y, out z);
            ground = Math.Max(ground, g.Height(x, z));
            f.ToWorld(u1, 0, w0 + want, out x, out y, out z);
            ground = Math.Max(ground, g.Height(x, z));
            f.ToWorld(um, 0, w0 + want, out x, out y, out z);
            ground = Math.Max(ground, g.Height(x, z));
            if (bottomY - ground >= RoadClearance.MinOverheadClearanceM) return want;
            return Math.Max(0.0, Math.Min(free, want));
        }

        /// <summary>
        /// The last guard of decision 2 over vertices <paramref name="v0"/>.. of a finished building: a vertex inside a
        /// corridor lower than the clearance above the ground under it (the frames, posts, bands and boxes a few
        /// centimetres proud of a wall that stands on the corridor edge) is moved out to the corridor edge plus
        /// <see cref="MarginM"/> along the distance gradient. Larger projections are clipped or raised by the builder
        /// (<see cref="Depth"/>) before this. Returns the vertices moved.
        /// </summary>
        public int Clamp(MeshData m, int v0, ref BuildingGround g)
        {
            int far;
            double push;
            return Clamp(m, v0, ref g, out far, out push);
        }

        /// <summary><see cref="Clamp(MeshData, int, ref BuildingGround)"/>, also counting the vertices moved further than
        /// 5 cm (<paramref name="far"/>: an element the builder should have clipped itself) and the longest push.</summary>
        public int Clamp(MeshData m, int v0, ref BuildingGround g, out int far, out double maxPush)
        {
            far = 0;
            maxPush = 0;
            if (_q == null) return 0;
            int moved = 0;
            float[] p = m.Positions;
            for (int v = v0 < 0 ? 0 : v0; v < m.VertexCount; v++)
            {
                int i = 3 * v;
                double x = p[i], y = p[i + 1], z = p[i + 2];
                double sd = _q.SignedDistance(_x0 + x, _z0 + z);
                if (sd >= -0.002) continue;
                if (y - g.Height(x, z) >= RoadClearance.MinOverheadClearanceM) continue;
                const double H = 0.05;
                double gx = _q.SignedDistance(_x0 + x + H, _z0 + z) - _q.SignedDistance(_x0 + x - H, _z0 + z);
                double gz = _q.SignedDistance(_x0 + x, _z0 + z + H) - _q.SignedDistance(_x0 + x, _z0 + z - H);
                double gl = Math.Sqrt(gx * gx + gz * gz);
                if (gl < 1e-9) continue;
                double push = -sd + MarginM;
                p[i] = (float)(x + gx / gl * push);
                p[i + 2] = (float)(z + gz / gl * push);
                moved++;
                if (-sd > 0.05) far++;
                if (push > maxPush) maxPush = push;
            }
            return moved;
        }

        /// <summary>True when an element of depth <paramref name="want"/> may project whole (no clipping).</summary>
        public bool Fits(in KitFrame f, double u0, double u1, double w0, double bottomY, double want, ref BuildingGround g)
        {
            return Depth(f, u0, u1, w0, bottomY, want, ref g) >= want - 1e-9;
        }

        /// <summary>The lowest absolute height at which an element over [u0, u1] projecting <paramref name="want"/> clears
        /// the overhead minimum above the ground in front of it.</summary>
        public double ClearY(in KitFrame f, double u0, double u1, double w0, double want, ref BuildingGround g)
        {
            double x, y, z, ground = double.MinValue;
            f.ToWorld(u0, 0, w0 + want, out x, out y, out z);
            ground = Math.Max(ground, g.Height(x, z));
            f.ToWorld(u1, 0, w0 + want, out x, out y, out z);
            ground = Math.Max(ground, g.Height(x, z));
            return ground + RoadClearance.MinOverheadClearanceM;
        }
    }
}
