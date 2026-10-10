using System;
using System.Collections.Generic;

namespace Ghumante.Core.Driving
{
    /// <summary>How a solid blocks (docs/W2_DETAIL_CONTRACT.md §1, W2_DESIGN 10.3).</summary>
    [Flags]
    public enum SolidFlags : byte
    {
        None = 0,

        /// <summary>A top at most the step-up above the feet is a step, not a wall (plinths, low walkable boxes); the
        /// ground query carries the body onto it.</summary>
        Steppable = 1,

        /// <summary>Ignored by camera casts (thin poles and trunks the chase camera may pass).</summary>
        NoCamera = 2,
    }

    /// <summary>
    /// One solid of the collision world: a vertical prism over a 2D capsule, the segment (<see cref="Ax"/>,
    /// <see cref="Az"/>)–(<see cref="Bx"/>, <see cref="Bz"/>) in game metres inflated by <see cref="Radius"/> (a cylinder
    /// when both ends coincide), spanning the absolute heights <see cref="Bottom"/> to <see cref="Top"/>. The edges of one
    /// outline (a building footprint, a box) share a <see cref="Group"/> (-1: none) so a body whose centre stands inside
    /// the outline (spawned or streamed in there) can always drive out of it.
    /// </summary>
    public struct SolidPrim
    {
        public double Ax, Az, Bx, Bz;
        public float Radius, Bottom, Top;
        public SolidFlags Flags;
        public int Group;
    }

    /// <summary>
    /// Collects solids and outline rings, then freezes them into a <see cref="SolidSet"/>. Coordinates are game metres.
    /// </summary>
    internal sealed class SolidBuilder
    {
        // Counter-clockwise box corners seen from above, in the box's own axes.
        private static readonly double[] CornerU = { -1, 1, 1, -1 }, CornerW = { -1, -1, 1, 1 };

        public readonly List<SolidPrim> Prims = new List<SolidPrim>();
        public readonly List<double> Vx = new List<double>(), Vz = new List<double>();
        public readonly List<int> RingStart = new List<int>(); // first vertex of every ring
        public readonly List<int> GroupRing = new List<int>(); // first ring of every group

        public int Count
        {
            get { return Prims.Count; }
        }

        public void Clear()
        {
            Prims.Clear();
            Vx.Clear();
            Vz.Clear();
            RingStart.Clear();
            GroupRing.Clear();
        }

        /// <summary>A vertical cylinder (tree trunk, pole, statue base).</summary>
        public void AddCylinder(double x, double z, float radius, float bottom, float top, SolidFlags flags)
        {
            if (!(radius > 0f) || !(top > bottom)) return;
            Prims.Add(new SolidPrim { Ax = x, Az = z, Bx = x, Bz = z, Radius = radius, Bottom = bottom, Top = top, Flags = flags, Group = -1 });
        }

        /// <summary>A wall from (ax, az) to (bx, bz) of half thickness <paramref name="halfThickness"/> (railings, parapets).</summary>
        public void AddWall(double ax, double az, double bx, double bz, float halfThickness, float bottom, float top, SolidFlags flags)
        {
            if (!(top > bottom) || halfThickness < 0f) return;
            Prims.Add(new SolidPrim { Ax = ax, Az = az, Bx = bx, Bz = bz, Radius = halfThickness, Bottom = bottom, Top = top, Flags = flags, Group = -1 });
        }

        /// <summary>Starts an outline group (rings follow with <see cref="AddRing"/>); returns its id.</summary>
        public int BeginGroup()
        {
            GroupRing.Add(RingStart.Count);
            return GroupRing.Count - 1;
        }

        /// <summary>Adds a closed ring (n ≥ 3 points) to the current group and its edges as walls.</summary>
        public void AddRing(double[] x, double[] z, int n, float edgeRadius, float bottom, float top, SolidFlags flags)
        {
            if (n < 3 || GroupRing.Count == 0) return;
            int g = GroupRing.Count - 1;
            RingStart.Add(Vx.Count);
            for (int i = 0; i < n; i++)
            {
                Vx.Add(x[i]);
                Vz.Add(z[i]);
            }
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                if (x[i] == x[j] && z[i] == z[j]) continue;
                Prims.Add(new SolidPrim
                {
                    Ax = x[i], Az = z[i], Bx = x[j], Bz = z[j], Radius = edgeRadius, Bottom = bottom, Top = top, Flags = flags, Group = g,
                });
            }
        }

        /// <summary>An oriented box (centre, half extents along its own axes, yaw counter-clockwise from +X seen from
        /// above) as a one-ring outline; <paramref name="margin"/> pads its walls.</summary>
        public void AddBox(double cx, double cz, double halfX, double halfZ, double yawRad, float bottom, float top, float margin,
                           SolidFlags flags, double[] scratchX, double[] scratchZ)
        {
            if (!(top > bottom)) return;
            double c = Math.Cos(yawRad), s = Math.Sin(yawRad);
            for (int k = 0; k < 4; k++)
            {
                double lx = CornerU[k] * halfX, lz = CornerW[k] * halfZ;
                scratchX[k] = cx + lx * c - lz * s;
                scratchZ[k] = cz + lx * s + lz * c;
            }
            BeginGroup();
            AddRing(scratchX, scratchZ, 4, margin, bottom, top, flags);
        }

        public SolidSet Build()
        {
            return new SolidSet(this);
        }
    }

    /// <summary>
    /// The solids of one tile (or one registered collider set) in game metres, bucketed in a uniform grid over their
    /// bounds. Queries never allocate: candidates are gathered into the set's own scratch, deduplicated with stamps.
    /// Read-only after construction; queries use per-set scratch, so one set is queried from one thread at a time (the
    /// ground query's thread).
    /// </summary>
    internal sealed class SolidSet
    {
        private const double MinCellM = 6.0;
        private const int MaxCellsPerSide = 160;
        private const double Eps = 1e-9;

        public readonly double MinX, MinZ, MaxX, MaxZ;
        private readonly SolidPrim[] _p;
        private readonly double[] _vx, _vz;
        private readonly int[] _ringStart; // ring r: vertices [_ringStart[r], _ringStart[r + 1])
        private readonly int[] _groupRing; // group g: rings [_groupRing[g], _groupRing[g + 1])
        private readonly int _nx, _nz;
        private readonly double _cell;
        private readonly int[] _cellStart, _items;
        private readonly int[] _stamp, _scratch;
        private readonly int[] _gStamp;
        private readonly bool[] _gInside;
        private readonly float[] _gBottom, _gTop; // vertical span of every group (from its walls)
        private readonly SolidFlags[] _gFlags;
        private readonly int[] _gCellStart, _gItems; // groups whose outline box touches each cell
        private int _query, _gQuery;

        public static readonly SolidSet Empty = new SolidBuilder().Build();

        public SolidSet(SolidBuilder b)
        {
            _p = b.Prims.ToArray();
            _vx = b.Vx.ToArray();
            _vz = b.Vz.ToArray();
            _ringStart = new int[b.RingStart.Count + 1];
            for (int i = 0; i < b.RingStart.Count; i++) _ringStart[i] = b.RingStart[i];
            _ringStart[b.RingStart.Count] = _vx.Length;
            _groupRing = new int[b.GroupRing.Count + 1];
            for (int i = 0; i < b.GroupRing.Count; i++) _groupRing[i] = b.GroupRing[i];
            _groupRing[b.GroupRing.Count] = b.RingStart.Count;
            _stamp = new int[_p.Length];
            _scratch = new int[_p.Length];
            int groups = b.GroupRing.Count;
            _gStamp = new int[groups];
            _gInside = new bool[groups];
            _gBottom = new float[groups];
            _gTop = new float[groups];
            _gFlags = new SolidFlags[groups];
            for (int g = 0; g < groups; g++)
            {
                _gBottom[g] = float.PositiveInfinity;
                _gTop[g] = float.NegativeInfinity;
            }
            for (int i = 0; i < _p.Length; i++)
            {
                int g = _p[i].Group;
                if (g < 0) continue;
                _gBottom[g] = Math.Min(_gBottom[g], _p[i].Bottom);
                _gTop[g] = Math.Max(_gTop[g], _p[i].Top);
                _gFlags[g] = _p[i].Flags;
            }

            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int i = 0; i < _p.Length; i++)
            {
                SolidPrim p = _p[i];
                minX = Math.Min(minX, Math.Min(p.Ax, p.Bx) - p.Radius);
                maxX = Math.Max(maxX, Math.Max(p.Ax, p.Bx) + p.Radius);
                minZ = Math.Min(minZ, Math.Min(p.Az, p.Bz) - p.Radius);
                maxZ = Math.Max(maxZ, Math.Max(p.Az, p.Bz) + p.Radius);
            }
            if (_p.Length == 0)
            {
                MinX = MinZ = MaxX = MaxZ = 0;
                _nx = _nz = 1;
                _cell = MinCellM;
                _cellStart = new int[2];
                _items = new int[0];
                _gCellStart = new int[2];
                _gItems = new int[0];
                return;
            }
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            double span = Math.Max(maxX - minX, maxZ - minZ);
            _cell = Math.Max(MinCellM, span / MaxCellsPerSide);
            _nx = Math.Max(1, (int)Math.Ceiling((maxX - minX) / _cell));
            _nz = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / _cell));
            _cellStart = new int[_nx * _nz + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = pass == 1 ? (int[])_cellStart.Clone() : null;
                for (int i = 0; i < _p.Length; i++)
                {
                    SolidPrim p = _p[i];
                    int cx0 = CellX(Math.Min(p.Ax, p.Bx) - p.Radius), cx1 = CellX(Math.Max(p.Ax, p.Bx) + p.Radius);
                    int cz0 = CellZ(Math.Min(p.Az, p.Bz) - p.Radius), cz1 = CellZ(Math.Max(p.Az, p.Bz) + p.Radius);
                    for (int cz = cz0; cz <= cz1; cz++)
                    for (int cx = cx0; cx <= cx1; cx++)
                    {
                        if (!SegmentTouchesCell(ref p, cx, cz)) continue;
                        if (pass == 0) _cellStart[cz * _nx + cx + 1]++;
                        else _items[fill[cz * _nx + cx]++] = i;
                    }
                }
                if (pass == 0)
                {
                    for (int c = 0; c < _nx * _nz; c++) _cellStart[c + 1] += _cellStart[c];
                    _items = new int[_cellStart[_nx * _nz]];
                }
            }

            // Groups by the box of their rings (for point-in-outline queries away from any wall).
            _gCellStart = new int[_nx * _nz + 1];
            var gx0 = new int[groups];
            var gx1 = new int[groups];
            var gz0 = new int[groups];
            var gz1 = new int[groups];
            for (int g = 0; g < groups; g++)
            {
                double ax = double.PositiveInfinity, az = double.PositiveInfinity, bx = double.NegativeInfinity, bz = double.NegativeInfinity;
                for (int r = _groupRing[g]; r < _groupRing[g + 1]; r++)
                for (int v = _ringStart[r]; v < _ringStart[r + 1]; v++)
                {
                    ax = Math.Min(ax, _vx[v]);
                    bx = Math.Max(bx, _vx[v]);
                    az = Math.Min(az, _vz[v]);
                    bz = Math.Max(bz, _vz[v]);
                }
                if (double.IsInfinity(ax))
                {
                    gx0[g] = 1;
                    gx1[g] = 0;
                    continue;
                }
                gx0[g] = CellX(ax);
                gx1[g] = CellX(bx);
                gz0[g] = CellZ(az);
                gz1[g] = CellZ(bz);
                for (int cz = gz0[g]; cz <= gz1[g]; cz++)
                for (int cx = gx0[g]; cx <= gx1[g]; cx++)
                    _gCellStart[cz * _nx + cx + 1]++;
            }
            for (int c = 0; c < _nx * _nz; c++) _gCellStart[c + 1] += _gCellStart[c];
            _gItems = new int[_gCellStart[_nx * _nz]];
            var gfill = new int[_nx * _nz];
            Array.Copy(_gCellStart, gfill, gfill.Length);
            for (int g = 0; g < groups; g++)
                for (int cz = gz0[g]; cz <= gz1[g]; cz++)
                for (int cx = gx0[g]; cx <= gx1[g]; cx++)
                    _gItems[gfill[cz * _nx + cx]++] = g;
        }

        /// <summary>Number of solids.</summary>
        public int Count
        {
            get { return _p.Length; }
        }

        /// <summary>Number of outline groups.</summary>
        public int GroupCount
        {
            get { return _gInside.Length; }
        }

        public SolidPrim this[int i]
        {
            get { return _p[i]; }
        }

        private int CellX(double x)
        {
            int c = (int)Math.Floor((x - MinX) / _cell);
            return c < 0 ? 0 : c >= _nx ? _nx - 1 : c;
        }

        private int CellZ(double z)
        {
            int c = (int)Math.Floor((z - MinZ) / _cell);
            return c < 0 ? 0 : c >= _nz ? _nz - 1 : c;
        }

        /// <summary>Conservative: the capsule's distance to the cell centre is within half the cell diagonal plus its radius.</summary>
        private bool SegmentTouchesCell(ref SolidPrim p, int cx, int cz)
        {
            double mx = MinX + (cx + 0.5) * _cell, mz = MinZ + (cz + 0.5) * _cell;
            double qx, qz;
            Closest(p.Ax, p.Az, p.Bx, p.Bz, mx, mz, out qx, out qz);
            double dx = mx - qx, dz = mz - qz;
            double reach = 0.7072 * _cell + p.Radius;
            return dx * dx + dz * dz <= reach * reach;
        }

        public bool Overlaps(double minX, double minZ, double maxX, double maxZ)
        {
            return _p.Length > 0 && maxX >= MinX && minX <= MaxX && maxZ >= MinZ && minZ <= MaxZ;
        }

        /// <summary>Gathers the solids whose cells touch the box into the scratch (deduplicated); returns the count.</summary>
        private int Gather(double minX, double minZ, double maxX, double maxZ)
        {
            if (!Overlaps(minX, minZ, maxX, maxZ)) return 0;
            if (++_query == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _query = 1;
            }
            int n = 0;
            int cx0 = CellX(minX), cx1 = CellX(maxX), cz0 = CellZ(minZ), cz1 = CellZ(maxZ);
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cz * _nx + cx;
                for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                {
                    int i = _items[k];
                    if (_stamp[i] == _query) continue;
                    _stamp[i] = _query;
                    _scratch[n++] = i;
                }
            }
            return n;
        }

        /// <summary>Starts a new point context for <see cref="Inside"/> (the cache is per point).</summary>
        private void NewPoint()
        {
            if (++_gQuery == int.MaxValue)
            {
                Array.Clear(_gStamp, 0, _gStamp.Length);
                _gQuery = 1;
            }
        }

        /// <summary>Even-odd point in outline over all rings of the group (holes count), cached per point context.</summary>
        private bool Inside(int g, double x, double z)
        {
            if (_gStamp[g] == _gQuery) return _gInside[g];
            bool inside = false;
            for (int r = _groupRing[g], rEnd = _groupRing[g + 1]; r < rEnd; r++)
            {
                int a = _ringStart[r], b = _ringStart[r + 1];
                for (int i = a, j = b - 1; i < b; j = i++)
                {
                    double zi = _vz[i], zj = _vz[j];
                    if (zi > z != zj > z && x < (_vx[j] - _vx[i]) * (z - zi) / (zj - zi) + _vx[i]) inside = !inside;
                }
            }
            _gStamp[g] = _gQuery;
            _gInside[g] = inside;
            return inside;
        }

        /// <summary>True when the body (feet at <paramref name="feetY"/>, height <paramref name="bodyH"/>) and the solid
        /// overlap vertically: the solid's top is above the feet (above the step-up for steppable solids) and its
        /// bottom below the head.</summary>
        public static bool Spans(in SolidPrim p, float feetY, float stepUp, float bodyH)
        {
            float low = (p.Flags & SolidFlags.Steppable) != 0 ? feetY + stepUp : feetY + 0.05f;
            return p.Top > low && p.Bottom < feetY + bodyH;
        }

        /// <summary>
        /// Sweeps circles (centres <paramref name="cx"/>, <paramref name="cz"/>, <paramref name="count"/> of them, all of
        /// radius <paramref name="r"/>) by (dx, dz) and lowers <paramref name="bestT"/> (fraction of the move, start 1)
        /// to the first contact, with the contact normal (unit, pointing from the solid towards the body). A circle
        /// already touching a solid and moving into it stops at t = 0; moving away is free. Circles whose centre lies
        /// inside an outline ignore that outline (they can always leave it).
        /// </summary>
        public bool Sweep(double[] cx, double[] cz, int count, float r, double dx, double dz, float feetY, float stepUp, float bodyH,
                          ref float bestT, ref float nx, ref float nz)
        {
            if (_p.Length == 0 || count <= 0) return false;
            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int k = 0; k < count; k++)
            {
                minX = Math.Min(minX, Math.Min(cx[k], cx[k] + dx));
                maxX = Math.Max(maxX, Math.Max(cx[k], cx[k] + dx));
                minZ = Math.Min(minZ, Math.Min(cz[k], cz[k] + dz));
                maxZ = Math.Max(maxZ, Math.Max(cz[k], cz[k] + dz));
            }
            int n = Gather(minX - r, minZ - r, maxX + r, maxZ + r);
            if (n == 0) return false;
            bool hit = false;
            for (int k = 0; k < count; k++)
            {
                NewPoint();
                double px = cx[k], pz = cz[k];
                for (int m = 0; m < n; m++)
                {
                    int i = _scratch[m];
                    SolidPrim p = _p[i];
                    if (!Spans(in p, feetY, stepUp, bodyH)) continue;
                    if (p.Group >= 0 && Inside(p.Group, px, pz)) continue;
                    float t, hx, hz;
                    if (!SweepCircle(px, pz, dx, dz, r + p.Radius, ref p, out t, out hx, out hz)) continue;
                    if (t < bestT)
                    {
                        bestT = t;
                        nx = hx;
                        nz = hz;
                        hit = true;
                    }
                }
            }
            return hit;
        }

        /// <summary>
        /// Penetration of circles into solids at rest: accumulates into (pushX, pushZ) the largest push out of any
        /// solid per axis direction (the deepest contact wins). Returns true when any circle overlaps.
        /// </summary>
        public bool Penetration(double[] cx, double[] cz, int count, float r, float feetY, float stepUp, float bodyH,
                                ref double pushX, ref double pushZ, ref double depth)
        {
            if (_p.Length == 0 || count <= 0) return false;
            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int k = 0; k < count; k++)
            {
                minX = Math.Min(minX, cx[k]);
                maxX = Math.Max(maxX, cx[k]);
                minZ = Math.Min(minZ, cz[k]);
                maxZ = Math.Max(maxZ, cz[k]);
            }
            int n = Gather(minX - r, minZ - r, maxX + r, maxZ + r);
            bool any = false;
            for (int k = 0; k < count; k++)
            {
                NewPoint();
                double px = cx[k], pz = cz[k];
                for (int m = 0; m < n; m++)
                {
                    SolidPrim p = _p[_scratch[m]];
                    if (!Spans(in p, feetY, stepUp, bodyH)) continue;
                    if (p.Group >= 0 && Inside(p.Group, px, pz)) continue;
                    double qx, qz;
                    Closest(p.Ax, p.Az, p.Bx, p.Bz, px, pz, out qx, out qz);
                    double ox = px - qx, oz = pz - qz;
                    double d = Math.Sqrt(ox * ox + oz * oz), rt = r + p.Radius;
                    if (d >= rt) continue;
                    double ux, uz;
                    if (d > 1e-6)
                    {
                        ux = ox / d;
                        uz = oz / d;
                    }
                    else
                    {
                        SegmentNormal(ref p, out ux, out uz);
                    }
                    double pen = rt - d;
                    if (pen > depth)
                    {
                        depth = pen;
                        pushX = ux * pen;
                        pushZ = uz * pen;
                    }
                    any = true;
                }
            }
            return any;
        }

        /// <summary>True when a circle at (x, z) overlaps a solid spanning the body.</summary>
        public bool Blocked(double x, double z, float r, float feetY, float stepUp, float bodyH)
        {
            int n = Gather(x - r, z - r, x + r, z + r);
            if (n == 0) return false;
            NewPoint();
            for (int m = 0; m < n; m++)
            {
                SolidPrim p = _p[_scratch[m]];
                if (!Spans(in p, feetY, stepUp, bodyH)) continue;
                if (p.Group >= 0 && Inside(p.Group, x, z)) continue;
                double qx, qz;
                Closest(p.Ax, p.Az, p.Bx, p.Bz, x, z, out qx, out qz);
                double ox = x - qx, oz = z - qz, rt = r + p.Radius;
                if (ox * ox + oz * oz < rt * rt) return true;
            }
            return false;
        }

        /// <summary>True when (x, z) lies inside a solid outline (a footprint) that spans the body, or within a solid's
        /// radius.</summary>
        public bool Contains(double x, double z, float feetY, float bodyH)
        {
            if (!Overlaps(x, z, x, z)) return false;
            NewPoint();
            int c = CellZ(z) * _nx + CellX(x);
            for (int k = _gCellStart[c], end = _gCellStart[c + 1]; k < end; k++)
            {
                int g = _gItems[k];
                var span = new SolidPrim { Bottom = _gBottom[g], Top = _gTop[g], Flags = _gFlags[g] };
                if (Spans(in span, feetY, 0f, bodyH) && Inside(g, x, z)) return true;
            }
            int n = Gather(x, z, x, z);
            for (int m = 0; m < n; m++)
            {
                SolidPrim p = _p[_scratch[m]];
                if (!Spans(in p, feetY, 0f, bodyH)) continue;
                double qx, qz;
                Closest(p.Ax, p.Az, p.Bx, p.Bz, x, z, out qx, out qz);
                double ox = x - qx, oz = z - qz;
                if (ox * ox + oz * oz < (double)p.Radius * p.Radius) return true;
            }
            return false;
        }

        /// <summary>
        /// Sphere cast for cameras: a sphere of radius <paramref name="r"/> from (ox, oy, oz) along (dx, dy, dz) × len
        /// (dx, dy, dz a unit vector). Lowers <paramref name="best"/> (a distance) to the first contact with a solid's
        /// prism. Solids the sphere starts in, and outlines containing the start, are ignored.
        /// </summary>
        public void SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, float r, double len, ref double best)
        {
            if (_p.Length == 0) return;
            double ex = ox + dx * len, ez = oz + dz * len;
            int n = Gather(Math.Min(ox, ex) - r, Math.Min(oz, ez) - r, Math.Max(ox, ex) + r, Math.Max(oz, ez) + r);
            if (n == 0) return;
            NewPoint();
            double hx = dx * len, hz = dz * len, vy = dy * len;
            for (int m = 0; m < n; m++)
            {
                SolidPrim p = _p[_scratch[m]];
                if ((p.Flags & SolidFlags.NoCamera) != 0) continue;
                if (p.Group >= 0 && Inside(p.Group, ox, oz)) continue;
                double rt = r + p.Radius;
                double qx, qz;
                Closest(p.Ax, p.Az, p.Bx, p.Bz, ox, oz, out qx, out qz);
                double sx = ox - qx, sz = oz - qz;
                bool startIn = sx * sx + sz * sz < rt * rt;
                double yLo = p.Bottom - r, yHi = p.Top + r;
                if (startIn && oy >= yLo && oy <= yHi) continue; // starts inside this solid
                // Horizontal interval inside the inflated capsule: [tIn, tOut] in fractions of len.
                double tIn, tOut;
                if (startIn)
                {
                    tIn = 0;
                }
                else
                {
                    float t0, nx0, nz0;
                    if (!RayCapsule(ox, oz, hx, hz, ref p, rt, out t0, out nx0, out nz0)) continue;
                    tIn = t0;
                }
                double qx1, qz1;
                Closest(p.Ax, p.Az, p.Bx, p.Bz, ex, ez, out qx1, out qz1);
                double fx = ex - qx1, fz = ez - qz1;
                if (fx * fx + fz * fz < rt * rt)
                {
                    tOut = 1;
                }
                else
                {
                    float t1, nx1, nz1;
                    if (!RayCapsule(ex, ez, -hx, -hz, ref p, rt, out t1, out nx1, out nz1)) tOut = 1;
                    else tOut = 1 - t1;
                }
                // Vertical interval inside [yLo, yHi].
                double tA, tB;
                if (Math.Abs(vy) < 1e-9)
                {
                    if (oy < yLo || oy > yHi) continue;
                    tA = 0;
                    tB = 1;
                }
                else
                {
                    double a = (yLo - oy) / vy, b = (yHi - oy) / vy;
                    tA = Math.Min(a, b);
                    tB = Math.Max(a, b);
                }
                double t = Math.Max(tIn, tA), tEnd = Math.Min(tOut, tB);
                if (t > tEnd || t < 0 || t > 1) continue;
                double dist = t * len;
                if (dist < best) best = dist;
            }
        }

        // ---- geometry ----

        /// <summary>Closest point of segment A–B to P.</summary>
        internal static void Closest(double ax, double az, double bx, double bz, double px, double pz, out double qx, out double qz)
        {
            double ex = bx - ax, ez = bz - az;
            double len2 = ex * ex + ez * ez;
            double t = len2 > Eps ? ((px - ax) * ex + (pz - az) * ez) / len2 : 0.0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            qx = ax + ex * t;
            qz = az + ez * t;
        }

        private static void SegmentNormal(ref SolidPrim p, out double ux, out double uz)
        {
            double ex = p.Bx - p.Ax, ez = p.Bz - p.Az, l = Math.Sqrt(ex * ex + ez * ez);
            if (l < 1e-9)
            {
                ux = 1;
                uz = 0;
                return;
            }
            ux = ez / l;
            uz = -ex / l;
        }

        /// <summary>A circle at P moving by D against a capsule of combined radius rt: the first contact in [0, 1].</summary>
        private static bool SweepCircle(double px, double pz, double dx, double dz, double rt, ref SolidPrim p, out float t, out float nx, out float nz)
        {
            double qx, qz;
            Closest(p.Ax, p.Az, p.Bx, p.Bz, px, pz, out qx, out qz);
            double ox = px - qx, oz = pz - qz;
            double d2 = ox * ox + oz * oz;
            if (d2 < rt * rt)
            {
                // Touching already: blocked only when moving further in.
                double d = Math.Sqrt(d2);
                double ux, uz;
                if (d > 1e-6)
                {
                    ux = ox / d;
                    uz = oz / d;
                }
                else
                {
                    SegmentNormal(ref p, out ux, out uz);
                    if (ux * dx + uz * dz > 0)
                    {
                        ux = -ux;
                        uz = -uz;
                    }
                }
                t = 0f;
                nx = (float)ux;
                nz = (float)uz;
                return ux * dx + uz * dz < 0;
            }
            return RayCapsule(px, pz, dx, dz, ref p, rt, out t, out nx, out nz);
        }

        /// <summary>First time t in [0, 1] at which P + t·D reaches distance rt of segment A–B (P starts outside).</summary>
        private static bool RayCapsule(double px, double pz, double dx, double dz, ref SolidPrim p, double rt, out float t, out float nx, out float nz)
        {
            t = 1f;
            nx = 0f;
            nz = 0f;
            double best = double.PositiveInfinity, bnx = 0, bnz = 0;
            double ex = p.Bx - p.Ax, ez = p.Bz - p.Az;
            double len2 = ex * ex + ez * ez;
            if (len2 > Eps)
            {
                double len = Math.Sqrt(len2);
                double ux = ex / len, uz = ez / len;
                double mx = -uz, mz = ux; // left normal
                double s0 = (px - p.Ax) * mx + (pz - p.Az) * mz;
                double sd = dx * mx + dz * mz;
                double tt = -1, side = 0;
                if (s0 >= rt && sd < 0)
                {
                    tt = (s0 - rt) / -sd;
                    side = 1;
                }
                else if (s0 <= -rt && sd > 0)
                {
                    tt = (-rt - s0) / sd;
                    side = -1;
                }
                if (tt >= 0 && tt <= 1)
                {
                    double along = (px + dx * tt - p.Ax) * ux + (pz + dz * tt - p.Az) * uz;
                    if (along >= 0 && along <= len)
                    {
                        best = tt;
                        bnx = side * mx;
                        bnz = side * mz;
                    }
                }
            }
            double a = dx * dx + dz * dz;
            if (a > 1e-12)
            {
                for (int end = 0; end < 2; end++)
                {
                    double cx = end == 0 ? p.Ax : p.Bx, cz = end == 0 ? p.Az : p.Bz;
                    if (end == 1 && len2 <= Eps) break;
                    double fx = px - cx, fz = pz - cz;
                    double b = fx * dx + fz * dz;
                    if (b >= 0) continue;
                    double c = fx * fx + fz * fz - rt * rt;
                    double disc = b * b - a * c;
                    if (disc < 0) continue;
                    double tt = (-b - Math.Sqrt(disc)) / a;
                    if (tt < 0) tt = 0;
                    if (tt > 1 || tt >= best) continue;
                    double hx = px + dx * tt - cx, hz = pz + dz * tt - cz;
                    double hl = Math.Sqrt(hx * hx + hz * hz);
                    if (hl < 1e-9) continue;
                    best = tt;
                    bnx = hx / hl;
                    bnz = hz / hl;
                }
            }
            if (double.IsPositiveInfinity(best)) return false;
            t = (float)best;
            nx = (float)bnx;
            nz = (float)bnz;
            return true;
        }
    }
}
