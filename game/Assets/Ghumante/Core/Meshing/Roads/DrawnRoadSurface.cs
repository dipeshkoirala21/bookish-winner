using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Roads
{
    /// <summary>
    /// The road surface of one tile exactly as the road mesher drew it (owner playtest #8, docs/W2_PLAYTEST_1.md: "on some
    /// roads, character feet look like they are being sucked in"): the up-facing triangles of the tile's roads layer
    /// (<see cref="RoadMesher.Build"/>: carriageways on their smoothed vertical profile with crown and camber, kerb tops,
    /// raised footpaths, medians, shoulders, junction caps with their kerb returns, roundabout rings, islands and
    /// splitters, police podiums, a lowered underpass in its cutting, decks at their absolute heights) in a uniform grid,
    /// so the ground query stands walkers and wheels on the very triangles the player sees instead of the terrain plus a
    /// lift over the raw polylines (which missed the smoothed profile, the crown, the caps and the corner footpaths by
    /// 3-30 cm on most of the sample and by metres at old-core junctions).
    /// <para>Built once per (tile, sampler) from the mesh the streamer uploads (<see cref="FromMesh"/>, on the build
    /// worker, before the layer's indices are rebased into upload chunks) or by meshing the roads itself
    /// (<see cref="Build"/>). Immutable afterwards: thread-safe queries that do not allocate. About 30 bytes per drawn
    /// vertex and triangle (under 1.5 MB for the densest kathmandu_core tile).</para>
    /// </summary>
    public sealed class DrawnRoadSurface
    {
        /// <summary>Grid cell size (m).</summary>
        public const double CellM = 4.0;

        /// <summary>Triangles whose unit normal has at least this upward component (slopes up to about 41°) are walking
        /// surface; steeper ones are the sides of a step (kerb faces and noses, the skirt down from a raised road edge to the
        /// terrain, retaining walls): a vehicle meets them as a step up or down, never as a ramp that would throw it into the
        /// air at speed.</summary>
        public const float MinUpNormal = 0.75f;

        /// <summary>A point this close (m) outside a triangle's edge still counts as on it (float rounding where a ribbon end
        /// and a cap share an edge; the drawn edge vertices themselves).</summary>
        public const double EdgeToleranceM = 0.02;

        private readonly double _x0, _z0; // tile origin (game metres): positions are tile-local x/z, absolute y
        private readonly float[] _p; // x, y, z per kept vertex
        private readonly int[] _tri; // 3 vertex indices per kept triangle
        private readonly byte[] _channel; // MaterialChannel per triangle
        private readonly double _gx0, _gz0; // grid origin (tile-local)
        private readonly int _nx, _nz;
        private readonly int[] _start, _items;

        /// <summary>Bounds of the drawn surface in game metres (empty surface: min > max).</summary>
        public readonly double MinX, MinZ, MaxX, MaxZ;

        /// <summary>Number of walking-surface triangles.</summary>
        public int TriangleCount
        {
            get { return _tri.Length / 3; }
        }

        /// <summary>Approximate heap size in bytes (diagnostics, budgets).</summary>
        public long Bytes
        {
            get { return 4L * (_p.Length + _tri.Length + _start.Length + _items.Length) + _channel.Length; }
        }

        /// <summary>
        /// The drawn surface from a roads layer (<paramref name="m"/>: tile-local x/z, absolute y, absolute indices) of the
        /// tile whose south-west corner is game (<paramref name="x0"/>, <paramref name="z0"/>). Only up-facing triangles are
        /// kept (<see cref="MinUpNormal"/>); the mesh is not referenced afterwards.
        /// </summary>
        public static DrawnRoadSurface FromMesh(MeshData m, double x0, double z0)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            return new DrawnRoadSurface(m, 0, m.IndexCount - m.IndexCount % 3, x0, z0);
        }

        /// <summary>The drawn surface of tile <paramref name="t"/> meshed here exactly as the streamer meshes it
        /// (<see cref="RoadMesher.Build"/> on the same sampler and options).</summary>
        public static DrawnRoadSurface Build(TileData t, IHeightSampler h, RoadOptions o)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            var m = new MeshData(4096, 12288);
            RoadMesher.Build(t, h, o ?? new RoadOptions(), m);
            return FromMesh(m, t.Tile.X0, t.Tile.Z0);
        }

        private DrawnRoadSurface(MeshData m, int firstIndex, int endIndex, double x0, double z0)
        {
            _x0 = x0;
            _z0 = z0;
            float[] pos = m.Positions;
            int[] idx = m.Indices;
            var remap = new int[Math.Max(1, m.VertexCount)];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            var keptP = new List<float>(m.VertexCount * 3 / 2 + 3);
            var keptT = new List<int>((endIndex - firstIndex) / 2 + 3);
            var keptC = new List<byte>((endIndex - firstIndex) / 6 + 1);
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int k = firstIndex; k + 2 < endIndex; k += 3)
            {
                int a = idx[k], b = idx[k + 1], c = idx[k + 2];
                if (a < 0 || b < 0 || c < 0 || a >= m.VertexCount || b >= m.VertexCount || c >= m.VertexCount) continue;
                double ax = pos[3 * a], ay = pos[3 * a + 1], az = pos[3 * a + 2];
                double ux = pos[3 * b] - ax, uy = pos[3 * b + 1] - ay, uz = pos[3 * b + 2] - az;
                double vx = pos[3 * c] - ax, vy = pos[3 * c + 1] - ay, vz = pos[3 * c + 2] - az;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (!(nl > 1e-9) || Math.Abs(ny) / nl < MinUpNormal) continue;
                if (double.IsNaN(ay) || float.IsNaN(pos[3 * b + 1]) || float.IsNaN(pos[3 * c + 1])) continue;
                keptT.Add(Keep(a, pos, remap, keptP));
                keptT.Add(Keep(b, pos, remap, keptP));
                keptT.Add(Keep(c, pos, remap, keptP));
                byte ch = (byte)MaterialChannel.Plain;
                if (m.HasUv0 && m.Uv0 != null && 2 * a < m.Uv0.Length) ch = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(m.Uv0[2 * a])));
                keptC.Add(ch);
                for (int q = 0; q < 3; q++)
                {
                    int v = idx[k + q];
                    minX = Math.Min(minX, pos[3 * v]);
                    maxX = Math.Max(maxX, pos[3 * v]);
                    minZ = Math.Min(minZ, pos[3 * v + 2]);
                    maxZ = Math.Max(maxZ, pos[3 * v + 2]);
                }
            }
            _p = keptP.ToArray();
            _tri = keptT.ToArray();
            _channel = keptC.ToArray();
            int tris = _channel.Length;
            if (tris == 0)
            {
                _nx = _nz = 1;
                _start = new int[2];
                _items = new int[0];
                MinX = MinZ = double.MaxValue;
                MaxX = MaxZ = double.MinValue;
                return;
            }
            MinX = x0 + minX;
            MaxX = x0 + maxX;
            MinZ = z0 + minZ;
            MaxZ = z0 + maxZ;
            _gx0 = minX - EdgeToleranceM;
            _gz0 = minZ - EdgeToleranceM;
            _nx = Math.Max(1, (int)Math.Ceiling((maxX - minX + 2 * EdgeToleranceM) / CellM));
            _nz = Math.Max(1, (int)Math.Ceiling((maxZ - minZ + 2 * EdgeToleranceM) / CellM));
            int cells = _nx * _nz;
            _start = new int[cells + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = null;
                if (pass == 1)
                {
                    for (int c = 0; c < cells; c++) _start[c + 1] += _start[c];
                    _items = new int[_start[cells]];
                    fill = new int[cells];
                    Array.Copy(_start, fill, cells);
                }
                for (int t = 0; t < tris; t++)
                {
                    float tx0 = Math.Min(_p[3 * _tri[3 * t]], Math.Min(_p[3 * _tri[3 * t + 1]], _p[3 * _tri[3 * t + 2]]));
                    float tx1 = Math.Max(_p[3 * _tri[3 * t]], Math.Max(_p[3 * _tri[3 * t + 1]], _p[3 * _tri[3 * t + 2]]));
                    float tz0 = Math.Min(_p[3 * _tri[3 * t] + 2], Math.Min(_p[3 * _tri[3 * t + 1] + 2], _p[3 * _tri[3 * t + 2] + 2]));
                    float tz1 = Math.Max(_p[3 * _tri[3 * t] + 2], Math.Max(_p[3 * _tri[3 * t + 1] + 2], _p[3 * _tri[3 * t + 2] + 2]));
                    int i0 = CellX(tx0 - EdgeToleranceM), i1 = CellX(tx1 + EdgeToleranceM);
                    int j0 = CellZ(tz0 - EdgeToleranceM), j1 = CellZ(tz1 + EdgeToleranceM);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int cell = j * _nx + i;
                        if (pass == 0) _start[cell + 1]++;
                        else _items[fill[cell]++] = t;
                    }
                }
            }
        }

        private static int Keep(int v, float[] pos, int[] remap, List<float> kept)
        {
            if (remap[v] >= 0) return remap[v];
            int n = kept.Count / 3;
            kept.Add(pos[3 * v]);
            kept.Add(pos[3 * v + 1]);
            kept.Add(pos[3 * v + 2]);
            remap[v] = n;
            return n;
        }

        private int CellX(double lx)
        {
            int c = (int)Math.Floor((lx - _gx0) / CellM);
            return c < 0 ? 0 : c >= _nx ? _nx - 1 : c;
        }

        private int CellZ(double lz)
        {
            int c = (int)Math.Floor((lz - _gz0) / CellM);
            return c < 0 ? 0 : c >= _nz ? _nz - 1 : c;
        }

        /// <summary>True when game (x, z) lies within the bounds of the drawn surface (a cheap pre-test).</summary>
        public bool InBounds(double x, double z)
        {
            return x >= MinX - EdgeToleranceM && x <= MaxX + EdgeToleranceM && z >= MinZ - EdgeToleranceM && z <= MaxZ + EdgeToleranceM;
        }

        /// <summary>
        /// The drawn road surface at game (x, z) for feet at <paramref name="nearY"/>: of every drawn level there at most
        /// <paramref name="stepUpM"/> above the feet (higher ones, a deck or a street over an underpass, are overhead), the
        /// one nearest to the feet (the street the feet are on where two drawn pieces overlap a metre apart; a kerb top or a
        /// ramp just above them where only that is drawn), the higher on a tie. +∞ or NaN feet take the highest, −∞ the
        /// lowest. False where
        /// nothing is drawn, or every drawn level there is overhead. Height, unit face normal and the material channel of
        /// the triangle.
        /// </summary>
        public bool TrySample(double x, double z, float nearY, float stepUpM, out float y, out float nx, out float ny, out float nz,
                              out MaterialChannel channel)
        {
            y = 0f;
            nx = 0f;
            ny = 1f;
            nz = 0f;
            channel = MaterialChannel.Plain;
            if (_items.Length == 0 || double.IsNaN(x) || double.IsNaN(z) || !InBounds(x, z)) return false;
            float limit = float.IsNaN(nearY) || float.IsInfinity(nearY) ? float.PositiveInfinity : nearY + stepUpM;
            double lx = x - _x0, lz = z - _z0;
            int cell = CellZ(lz) * _nx + CellX(lx);
            bool highest = float.IsPositiveInfinity(limit) && !float.IsNegativeInfinity(nearY), lowest = float.IsNegativeInfinity(nearY);
            int best = -1;
            float bestY = float.NegativeInfinity, bestD = float.PositiveInfinity;
            // Strictly inside first; within EdgeToleranceM of an edge only where no triangle holds the point.
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                double tol = pass == 0 ? 0.0 : EdgeToleranceM;
                for (int k = _start[cell], end = _start[cell + 1]; k < end; k++)
                {
                    int t = _items[k];
                    float h;
                    if (!HeightOn(t, lx, lz, tol, out h) || h > limit) continue;
                    float d = highest ? -h : lowest ? h : Math.Abs(h - nearY);
                    if (best >= 0 && (d > bestD || d == bestD && !(h > bestY))) continue;
                    best = t;
                    bestY = h;
                    bestD = d;
                }
            }
            if (best < 0) return false;
            y = bestY;
            channel = (MaterialChannel)_channel[best];
            FaceNormal(best, out nx, out ny, out nz);
            return true;
        }

        /// <summary>Every drawn level over game (x, z) (strictly inside a triangle), ascending, into
        /// <paramref name="into"/> (levels within 0.05 m merged to the higher); returns how many (at most its length).</summary>
        public int Levels(double x, double z, float[] into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (_items.Length == 0 || !InBounds(x, z) || into.Length == 0) return 0;
            double lx = x - _x0, lz = z - _z0;
            int cell = CellZ(lz) * _nx + CellX(lx), n = 0;
            for (int k = _start[cell], end = _start[cell + 1]; k < end; k++)
            {
                float h;
                if (!HeightOn(_items[k], lx, lz, 0.0, out h)) continue;
                int at = n;
                bool merged = false;
                for (int q = 0; q < n; q++)
                {
                    if (Math.Abs(into[q] - h) < 0.05f)
                    {
                        into[q] = Math.Max(into[q], h);
                        merged = true;
                        break;
                    }
                }
                if (merged || n >= into.Length) continue;
                while (at > 0 && into[at - 1] > h)
                {
                    into[at] = into[at - 1];
                    at--;
                }
                into[at] = h;
                n++;
            }
            return n;
        }

        private bool HeightOn(int t, double lx, double lz, double tol, out float h)
        {
            h = 0f;
            int a = 3 * _tri[3 * t], b = 3 * _tri[3 * t + 1], c = 3 * _tri[3 * t + 2];
            double ax = _p[a], az = _p[a + 2], bx = _p[b], bz = _p[b + 2], cx = _p[c], cz = _p[c + 2];
            double d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
            if (Math.Abs(d) < 1e-12) return false;
            double w0 = ((bz - cz) * (lx - cx) + (cx - bx) * (lz - cz)) / d;
            double w1 = ((cz - az) * (lx - cx) + (ax - cx) * (lz - cz)) / d;
            double w2 = 1.0 - w0 - w1;
            if (tol <= 0)
            {
                const double eps = -1e-9;
                if (w0 < eps || w1 < eps || w2 < eps) return false;
            }
            else
            {
                // Distance outside each edge: w_i × (2 × area) / |opposite edge|.
                double area2 = Math.Abs(d);
                if (w0 < 0 && w0 * area2 / Len(bx, bz, cx, cz) < -tol) return false;
                if (w1 < 0 && w1 * area2 / Len(cx, cz, ax, az) < -tol) return false;
                if (w2 < 0 && w2 * area2 / Len(ax, az, bx, bz) < -tol) return false;
                w0 = Math.Max(0, w0);
                w1 = Math.Max(0, w1);
                w2 = Math.Max(0, w2);
                double s = w0 + w1 + w2;
                w0 /= s;
                w1 /= s;
                w2 /= s;
            }
            h = (float)(w0 * _p[a + 1] + w1 * _p[b + 1] + w2 * _p[c + 1]);
            return true;
        }

        private static double Len(double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            return Math.Max(1e-9, Math.Sqrt(dx * dx + dz * dz));
        }

        private void FaceNormal(int t, out float nx, out float ny, out float nz)
        {
            int a = 3 * _tri[3 * t], b = 3 * _tri[3 * t + 1], c = 3 * _tri[3 * t + 2];
            double ux = _p[b] - _p[a], uy = _p[b + 1] - _p[a + 1], uz = _p[b + 2] - _p[a + 2];
            double vx = _p[c] - _p[a], vy = _p[c + 1] - _p[a + 1], vz = _p[c + 2] - _p[a + 2];
            double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
            if (y < 0)
            {
                x = -x;
                y = -y;
                z = -z;
            }
            double l = Math.Sqrt(x * x + y * y + z * z);
            if (!(l > 1e-12))
            {
                nx = 0f;
                ny = 1f;
                nz = 0f;
                return;
            }
            nx = (float)(x / l);
            ny = (float)(y / l);
            nz = (float)(z / l);
        }
    }
}
