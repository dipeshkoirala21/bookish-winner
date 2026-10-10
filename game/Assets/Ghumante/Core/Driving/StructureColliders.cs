using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;

namespace Ghumante.Core.Driving
{
    /// <summary>How a structure collider behaves (W2_DESIGN 10.3).</summary>
    [Flags]
    public enum ColliderFlags : byte
    {
        None = 0,

        /// <summary>The top is walkable ground (plinths, pikha aprons, terraces, squares, decks).</summary>
        Walkable = 1,

        /// <summary>Never climbable: blocks at every height it spans (roofs, domes, spires, temple walls).</summary>
        NoClimb = 2,

        /// <summary>Keep a gentle distance (<see cref="StructureColliders.SoftMarginM"/>) from the box (temple walls).</summary>
        SoftMargin = 4,
    }

    /// <summary>
    /// An oriented box: centre (<see cref="CX"/>, <see cref="CZ"/>) in tile-local metres from the tile's south-west
    /// corner (game metres once registered), centre height <see cref="CY"/> absolute, half extents along its own axes.
    /// Its local X axis points along <see cref="YawRad"/> measured counter-clockwise from game +X (east) seen from above,
    /// so local X = (cos yaw, sin yaw) in (X, Z), as <c>GenBox</c>.
    /// </summary>
    public struct OrientedBox
    {
        public double CX, CZ;
        public float CY, HalfX, HalfY, HalfZ, YawRad;
        public ColliderFlags Flags;

        /// <summary>What a foot on the top touches (plinths and squares Stone, Bhaktapur and Patan paving Brick, dabali
        /// and balconies Wood, Bailey decks Metal).</summary>
        public FootSurface Material;

        public float Top
        {
            get { return CY + HalfY; }
        }

        public float Bottom
        {
            get { return CY - HalfY; }
        }
    }

    /// <summary>A walkable ramp (a stair drawn with visual steps) from (X0, Z0, Y0) to (X1, Z1, Y1), tile-local metres
    /// (game metres once registered) with absolute heights, <see cref="HalfWidth"/> to each side of its axis.</summary>
    public struct StepRamp
    {
        public double X0, Z0, X1, Z1;
        public float Y0, Y1, HalfWidth;
        public FootSurface Material;
    }

    /// <summary>A vertical cylinder (tree trunk, pole, statue, bollard, pier) centred at (<see cref="CX"/>,
    /// <see cref="CZ"/>) in tile-local metres (game metres once registered), spanning the absolute heights
    /// <see cref="Bottom"/> to <see cref="Top"/>. Never walkable: it blocks every body it spans. It stops chase cameras
    /// too, unless <see cref="CameraPasses"/> or it is thinner than <see cref="StructureColliders.ThinCylinderM"/>.</summary>
    public struct ColliderCylinder
    {
        public double CX, CZ;
        public float Radius, Bottom, Top;

        /// <summary>The chase camera passes through it (tree trunks, poles).</summary>
        public bool CameraPasses;
    }

    /// <summary>A thin wall (railing, parapet, fence) from (X0, Z0) to (X1, Z1) in tile-local metres (game metres once
    /// registered), <see cref="HalfThickness"/> to each side of its axis, spanning <see cref="Bottom"/> to
    /// <see cref="Top"/> (absolute).</summary>
    public struct ColliderWall
    {
        public double X0, Z0, X1, Z1;
        public float HalfThickness, Bottom, Top;
    }

    /// <summary>
    /// Colliders produced with a mesh in the same build job (sacred generators, house plinths, squares), in tile-local
    /// metres. They join the ground query when the mesh becomes visible (<see cref="IStructureGround.Register"/>) and
    /// leave it when the mesh is hidden, exactly as W1 areas do. Reusable: <see cref="Clear"/> keeps the capacity.
    /// Besides boxes and ramps it carries solid-only shapes for the instanced dressing (detail pass, collide package):
    /// <see cref="Cylinders"/> (tree trunks, poles, statues) and <see cref="Walls"/> (railings, parapets); helpers
    /// <see cref="AddTree"/>, <see cref="AddPole"/> and <see cref="AddVehicle"/> size them from the placement output.
    /// <para><b>Wiring (integration package).</b> Nothing here reaches the game unless the streamer passes it on:
    /// copy generator output with <see cref="AddFrom"/> (it keeps <c>GenColliders.Cylinders</c> and <c>.Walls</c>, which
    /// a copy of only the boxes and ramps drops) and merge per tile with <see cref="AddAll"/>; register a tile's colliders
    /// also when it has only cylinders or walls (test <see cref="Count"/>, not the box and ramp counts); and add the
    /// placed dressing that has no PROP record (generated trees: <see cref="AddTree"/>; parked vehicles:
    /// <see cref="AddVehicle"/>) to the tile's set before <see cref="IStructureGround.Register"/>.</para>
    /// </summary>
    public sealed class StructureColliders
    {
        /// <summary>Distance kept from <see cref="ColliderFlags.SoftMargin"/> boxes (W2_DESIGN 3.3: 0.8 m around
        /// temple walls).</summary>
        public const float SoftMarginM = 0.8f;

        /// <summary>Tree trunk radius per metre of tree height, and its bounds (a cartoon trunk is a little fat).</summary>
        public const float TrunkRadiusPerM = 0.025f, MinTrunkRadiusM = 0.2f, MaxTrunkRadiusM = 0.65f;

        /// <summary>Cylinders thinner than this (bollards, posts, sign poles) never stop a chase camera; statues, columns
        /// and piers do.</summary>
        public const float ThinCylinderM = 0.3f;

        public readonly List<OrientedBox> Boxes = new List<OrientedBox>();
        public readonly List<StepRamp> Ramps = new List<StepRamp>();
        public readonly List<ColliderCylinder> Cylinders = new List<ColliderCylinder>();
        public readonly List<ColliderWall> Walls = new List<ColliderWall>();

        public int Count
        {
            get { return Boxes.Count + Ramps.Count + Cylinders.Count + Walls.Count; }
        }

        public void Clear()
        {
            Boxes.Clear();
            Ramps.Clear();
            Cylinders.Clear();
            Walls.Clear();
        }

        /// <summary>Appends every collider of <paramref name="other"/> (same frame).</summary>
        public void AddAll(StructureColliders other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Boxes.AddRange(other.Boxes);
            Ramps.AddRange(other.Ramps);
            Cylinders.AddRange(other.Cylinders);
            Walls.AddRange(other.Walls);
        }

        public void AddCylinder(in ColliderCylinder c)
        {
            if (!(c.Radius > 0f) || !(c.Top > c.Bottom)) throw new ArgumentException("cylinder needs a positive radius and height");
            Cylinders.Add(c);
        }

        public void AddWall(in ColliderWall w)
        {
            if (!(w.HalfThickness >= 0f) || !(w.Top > w.Bottom)) throw new ArgumentException("wall needs a thickness and height");
            Walls.Add(w);
        }

        /// <summary>A tree's trunk at (x, z) on ground <paramref name="groundY"/>: radius from the height (bounded by
        /// <see cref="MinTrunkRadiusM"/>..<see cref="MaxTrunkRadiusM"/>), solid from below the ground to half the tree
        /// (at least 2.5 m), so the crown never snags a bus but the trunk stops everything.</summary>
        public void AddTree(double x, double z, float groundY, float heightM)
        {
            float h = heightM > 0f ? heightM : 8f;
            float r = Math.Min(MaxTrunkRadiusM, Math.Max(MinTrunkRadiusM, h * TrunkRadiusPerM));
            Cylinders.Add(new ColliderCylinder
            {
                CX = x, CZ = z, Radius = r, Bottom = groundY - 0.5f, Top = groundY + Math.Max(2.5f, 0.5f * h), CameraPasses = true,
            });
        }

        /// <summary>A pole, mast or post of radius <paramref name="radiusM"/> and height <paramref name="heightM"/>.</summary>
        public void AddPole(double x, double z, float groundY, float heightM, float radiusM)
        {
            if (!(radiusM > 0f)) radiusM = 0.12f;
            Cylinders.Add(new ColliderCylinder
            {
                CX = x, CZ = z, Radius = radiusM, Bottom = groundY - 0.3f, Top = groundY + Math.Max(1f, heightM), CameraPasses = true,
            });
        }

        /// <summary>A parked vehicle of catalogue <paramref name="variant"/> at (x, z) (the mesh origin, on the ground under
        /// the rear axle) facing <paramref name="yawDegCwFromNorth"/>: a no-climb box of the catalogue body.</summary>
        public void AddVehicle(double x, double z, float groundY, float yawDegCwFromNorth, int variant)
        {
            if (variant < 0 || variant >= VehicleCatalog.Count) return;
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            double yaw = yawDegCwFromNorth * (Math.PI / 180.0);
            double fx = Math.Sin(yaw), fz = Math.Cos(yaw);
            double mid = 0.5 * e.LengthM - e.RearOverhangM;
            Boxes.Add(new OrientedBox
            {
                CX = x + fx * mid, CZ = z + fz * mid, CY = groundY + 0.5f * e.HeightM, HalfX = 0.5f * e.LengthM, HalfY = 0.5f * e.HeightM,
                HalfZ = 0.5f * e.WidthM, YawRad = (float)Math.Atan2(fz, fx), Flags = ColliderFlags.NoClimb, Material = FootSurface.Metal,
            });
        }

        public void AddBox(in OrientedBox b)
        {
            if (!(b.HalfX >= 0f) || !(b.HalfY >= 0f) || !(b.HalfZ >= 0f)) throw new ArgumentException("box half extents must be >= 0");
            Boxes.Add(b);
        }

        public void AddRamp(in StepRamp r)
        {
            if (!(r.HalfWidth > 0f)) throw new ArgumentException("ramp half width must be positive");
            Ramps.Add(r);
        }

        /// <summary>Appends Track A's generator output (same frame and fields; material codes are
        /// <see cref="FootSurface"/> values), including its solid cylinders and walls.</summary>
        public void AddFrom(GenColliders g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            foreach (GenBox b in g.Boxes)
            {
                Boxes.Add(new OrientedBox
                {
                    CX = b.CX, CZ = b.CZ, CY = b.CY, HalfX = b.HalfX, HalfY = b.HalfY, HalfZ = b.HalfZ, YawRad = b.YawRad,
                    Flags = (ColliderFlags)(byte)b.Flags, Material = MaterialOf(b.Material),
                });
            }
            foreach (GenRamp r in g.Ramps)
            {
                if (!(r.HalfWidth > 0f)) continue;
                Ramps.Add(new StepRamp
                {
                    X0 = r.X0, Z0 = r.Z0, X1 = r.X1, Z1 = r.Z1, Y0 = r.Y0, Y1 = r.Y1, HalfWidth = r.HalfWidth,
                    Material = MaterialOf(r.Material),
                });
            }
            foreach (GenCylinder y in g.Cylinders)
            {
                if (!(y.Radius > 0f) || !(y.Y1 > y.Y0)) continue;
                Cylinders.Add(new ColliderCylinder { CX = y.CX, CZ = y.CZ, Radius = y.Radius, Bottom = y.Y0, Top = y.Y1, CameraPasses = y.CameraPasses });
            }
            foreach (GenWall w in g.Walls)
            {
                if (!(w.HalfThickness >= 0f) || !(w.Y1 > w.Y0)) continue;
                Walls.Add(new ColliderWall { X0 = w.X0, Z0 = w.Z0, X1 = w.X1, Z1 = w.Z1, HalfThickness = w.HalfThickness, Bottom = w.Y0, Top = w.Y1 });
            }
        }

        private static FootSurface MaterialOf(byte code)
        {
            return code < FootSurfaces.Count ? (FootSurface)code : FootSurface.Stone;
        }
    }

    /// <summary>A ground query that structure colliders can join (W2_DESIGN 10.3): <see cref="TileGroundQuery"/>.</summary>
    public interface IStructureGround
    {
        /// <summary>Adds (or replaces) the colliders of a tile (tile-local coordinates of <paramref name="tileKey"/>).
        /// The colliders are copied: the caller may reuse <paramref name="c"/>.</summary>
        void Register(ulong tileKey, StructureColliders c);

        void Unregister(ulong tileKey);
    }

    /// <summary>
    /// The registered structure colliders of one tile in game metres, bucketed in a uniform grid over their bounds for
    /// allocation-free point queries. Built once on register; read-only afterwards.
    /// </summary>
    internal sealed class StructureSet
    {
        private const double CellM = 8.0;
        private const int MaxCellsPerSide = 128;

        public double MinX, MinZ, MaxX, MaxZ;
        private readonly OrientedBox[] _boxes;
        private readonly StepRamp[] _ramps;
        private readonly double[] _cos, _sin; // per box
        private readonly int _nx, _nz;
        private readonly double _cell;
        private readonly int[] _cellStart, _items; // item = box index, or ~ramp index

        /// <summary>Boxes, cylinders and walls as solids (game metres) for swept bodies and cameras.</summary>
        public readonly SolidSet Solids;

        public StructureSet(ulong tileKey, StructureColliders c, float margin)
        {
            TileId id = TileId.FromKey(tileKey);
            double x0 = id.X0, z0 = id.Z0;
            Solids = BuildSolids(c, x0, z0);
            _boxes = new OrientedBox[c.Boxes.Count];
            _ramps = new StepRamp[c.Ramps.Count];
            _cos = new double[_boxes.Length];
            _sin = new double[_boxes.Length];
            MinX = MinZ = double.PositiveInfinity;
            MaxX = MaxZ = double.NegativeInfinity;
            var bx0 = new double[_boxes.Length + _ramps.Length];
            var bz0 = new double[bx0.Length];
            var bx1 = new double[bx0.Length];
            var bz1 = new double[bx0.Length];
            for (int i = 0; i < _boxes.Length; i++)
            {
                OrientedBox b = c.Boxes[i];
                b.CX += x0;
                b.CZ += z0;
                _boxes[i] = b;
                _cos[i] = Math.Cos(b.YawRad);
                _sin[i] = Math.Sin(b.YawRad);
                double ex = Math.Abs(_cos[i]) * b.HalfX + Math.Abs(_sin[i]) * b.HalfZ + margin;
                double ez = Math.Abs(_sin[i]) * b.HalfX + Math.Abs(_cos[i]) * b.HalfZ + margin;
                bx0[i] = b.CX - ex;
                bx1[i] = b.CX + ex;
                bz0[i] = b.CZ - ez;
                bz1[i] = b.CZ + ez;
            }
            for (int k = 0; k < _ramps.Length; k++)
            {
                StepRamp r = c.Ramps[k];
                r.X0 += x0;
                r.X1 += x0;
                r.Z0 += z0;
                r.Z1 += z0;
                _ramps[k] = r;
                int i = _boxes.Length + k;
                bx0[i] = Math.Min(r.X0, r.X1) - r.HalfWidth;
                bx1[i] = Math.Max(r.X0, r.X1) + r.HalfWidth;
                bz0[i] = Math.Min(r.Z0, r.Z1) - r.HalfWidth;
                bz1[i] = Math.Max(r.Z0, r.Z1) + r.HalfWidth;
            }
            for (int i = 0; i < bx0.Length; i++)
            {
                MinX = Math.Min(MinX, bx0[i]);
                MinZ = Math.Min(MinZ, bz0[i]);
                MaxX = Math.Max(MaxX, bx1[i]);
                MaxZ = Math.Max(MaxZ, bz1[i]);
            }
            if (bx0.Length == 0)
            {
                MinX = MinZ = MaxX = MaxZ = 0;
                _nx = _nz = 1;
                _cell = CellM;
                _cellStart = new int[2];
                _items = new int[0];
                return;
            }
            double span = Math.Max(MaxX - MinX, MaxZ - MinZ);
            _cell = Math.Max(CellM, span / MaxCellsPerSide);
            _nx = Math.Max(1, (int)Math.Ceiling((MaxX - MinX) / _cell));
            _nz = Math.Max(1, (int)Math.Ceiling((MaxZ - MinZ) / _cell));
            _cellStart = new int[_nx * _nz + 1];
            for (int i = 0; i < bx0.Length; i++)
                for (int cz = Cell(bz0[i], MinZ, _nz); cz <= Cell(bz1[i], MinZ, _nz); cz++)
                for (int cx = Cell(bx0[i], MinX, _nx); cx <= Cell(bx1[i], MinX, _nx); cx++)
                    _cellStart[cz * _nx + cx + 1]++;
            for (int k = 0; k < _nx * _nz; k++) _cellStart[k + 1] += _cellStart[k];
            _items = new int[_cellStart[_nx * _nz]];
            var fill = new int[_nx * _nz];
            Array.Copy(_cellStart, fill, fill.Length);
            for (int i = 0; i < bx0.Length; i++)
                for (int cz = Cell(bz0[i], MinZ, _nz); cz <= Cell(bz1[i], MinZ, _nz); cz++)
                for (int cx = Cell(bx0[i], MinX, _nx); cx <= Cell(bx1[i], MinX, _nx); cx++)
                    _items[fill[cz * _nx + cx]++] = i < _boxes.Length ? i : ~(i - _boxes.Length);
        }

        /// <summary>The solid view of a collider set: every box as a one-ring outline (walkable boxes are steppable,
        /// soft-margin boxes padded by <see cref="StructureColliders.SoftMarginM"/>), cylinders and walls as they are
        /// (cameras pass trunks, poles and anything thinner than <see cref="StructureColliders.ThinCylinderM"/>).</summary>
        private static SolidSet BuildSolids(StructureColliders c, double x0, double z0)
        {
            if (c.Boxes.Count + c.Cylinders.Count + c.Walls.Count == 0) return SolidSet.Empty;
            var b = new SolidBuilder();
            var sx = new double[4];
            var sz = new double[4];
            foreach (OrientedBox o in c.Boxes)
            {
                bool walk = (o.Flags & ColliderFlags.Walkable) != 0 && (o.Flags & ColliderFlags.NoClimb) == 0;
                float margin = (o.Flags & ColliderFlags.SoftMargin) != 0 ? StructureColliders.SoftMarginM : 0f;
                b.AddBox(x0 + o.CX, z0 + o.CZ, o.HalfX, o.HalfZ, o.YawRad, o.Bottom, o.Top, margin, walk ? SolidFlags.Steppable : SolidFlags.None, sx, sz);
            }
            foreach (ColliderCylinder y in c.Cylinders)
            {
                bool cameraPasses = y.CameraPasses || y.Radius < StructureColliders.ThinCylinderM;
                b.AddCylinder(x0 + y.CX, z0 + y.CZ, y.Radius, y.Bottom, y.Top, cameraPasses ? SolidFlags.NoCamera : SolidFlags.None);
            }
            foreach (ColliderWall w in c.Walls) b.AddWall(x0 + w.X0, z0 + w.Z0, x0 + w.X1, z0 + w.Z1, w.HalfThickness, w.Bottom, w.Top, SolidFlags.None);
            return b.Build();
        }

        private int Cell(double v, double min, int n)
        {
            int c = (int)Math.Floor((v - min) / _cell);
            return c < 0 ? 0 : c >= n ? n - 1 : c;
        }

        public bool Covers(double x, double z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        /// <summary>
        /// The structure ground at (x, z) for someone whose feet are at <paramref name="nearY"/>: the highest walkable box
        /// top or ramp height at most <paramref name="stepUp"/> above the feet (any height when nearY is +∞), and whether
        /// a box blocks the body there (a walkable top too high to step onto, or a no-climb box spanning the body; only
        /// for a finite nearY). <paramref name="best"/> is raised, never lowered.
        /// </summary>
        public void Query(double x, double z, float nearY, float stepUp, float bodyH, float bodyR, ref float best, ref FootSurface foot,
                          ref float gradeX, ref float gradeZ, ref bool found, ref bool blocked)
        {
            if (_items.Length == 0) return;
            int cx = Cell(x, MinX, _nx), cz = Cell(z, MinZ, _nz);
            int c = cz * _nx + cx;
            bool finite = !float.IsInfinity(nearY);
            float reach = finite ? nearY + stepUp : float.PositiveInfinity;
            for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
            {
                int it = _items[k];
                if (it >= 0)
                {
                    OrientedBox b = _boxes[it];
                    double dx = x - b.CX, dz = z - b.CZ;
                    double lx = dx * _cos[it] + dz * _sin[it], lz = -dx * _sin[it] + dz * _cos[it];
                    double ax = Math.Abs(lx), az = Math.Abs(lz);
                    bool inside = ax <= b.HalfX && az <= b.HalfZ;
                    float top = b.Top;
                    bool walk = (b.Flags & ColliderFlags.Walkable) != 0 && (b.Flags & ColliderFlags.NoClimb) == 0;
                    if (walk && inside && top <= reach)
                    {
                        if (!found || top > best)
                        {
                            best = top;
                            foot = b.Material;
                            gradeX = 0f;
                            gradeZ = 0f;
                        }
                        found = true;
                        continue;
                    }
                    if (!finite) continue;
                    // Blocking: the body (radius bodyR, plus the soft margin) overlaps a box that spans the body's height.
                    float m = bodyR + ((b.Flags & ColliderFlags.SoftMargin) != 0 ? StructureColliders.SoftMarginM : 0f);
                    if (ax > b.HalfX + m || az > b.HalfZ + m) continue;
                    float lowBlock = walk ? reach : nearY + 0.05f;
                    if (top > lowBlock && b.Bottom < nearY + bodyH) blocked = true;
                }
                else
                {
                    StepRamp r = _ramps[~it];
                    double ux = r.X1 - r.X0, uz = r.Z1 - r.Z0;
                    double len2 = ux * ux + uz * uz;
                    if (len2 < 1e-9) continue;
                    double t = ((x - r.X0) * ux + (z - r.Z0) * uz) / len2;
                    if (t < 0 || t > 1) continue;
                    double len = Math.Sqrt(len2);
                    double lat = ((x - r.X0) * uz - (z - r.Z0) * ux) / len;
                    if (Math.Abs(lat) > r.HalfWidth) continue;
                    float hgt = (float)(r.Y0 + (r.Y1 - r.Y0) * t);
                    if (hgt > reach) continue;
                    if (!found || hgt > best)
                    {
                        best = hgt;
                        foot = r.Material;
                        float g = (float)((r.Y1 - r.Y0) / len);
                        gradeX = (float)(g * ux / len);
                        gradeZ = (float)(g * uz / len);
                    }
                    found = true;
                }
            }
        }

        public int BoxCount
        {
            get { return _boxes.Length; }
        }

        public int RampCount
        {
            get { return _ramps.Length; }
        }
    }
}
