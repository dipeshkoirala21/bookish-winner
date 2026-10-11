using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Horizon
{
    /// <summary>
    /// One curated summit as the skyline draws it: a cone to the published height. <see cref="Synthetic"/> cones stand
    /// in for whole massifs beyond the region's horizon tiles (steeper faces between three long ridges, on a broad
    /// shoulder), the others only top the DEM's flattened summit.
    /// </summary>
    public struct HorizonCone
    {
        public double X, Z;
        public float HeightM;

        /// <summary>Height lost per metre from the summit (between the ridges for a synthetic massif).</summary>
        public float Slope;

        /// <summary>Beyond this horizontal distance the cone adds nothing.</summary>
        public float ReachM;

        public bool Synthetic;

        /// <summary>Directions of the three ridges of a synthetic massif, radians clockwise from north.</summary>
        public float Ridge0, Ridge1, Ridge2;

        /// <summary>Lowest height a synthetic massif draws (its foot).</summary>
        public const float FootM = 3000f;

        /// <summary>Height of the cone at horizontal distance <paramref name="r"/> in direction <paramref name="theta"/>
        /// (radians clockwise from north), or <see cref="float.MinValue"/> beyond its reach.</summary>
        public float HeightAt(double r, double theta)
        {
            if (r >= ReachM) return float.MinValue;
            if (!Synthetic) return (float)(HeightM - Slope * r);
            // Ridges fall at half the slope between them: m = 0 on a ridge, 1 sixty degrees away from every ridge.
            double m = Math.Min(AngleGap(theta, Ridge0), Math.Min(AngleGap(theta, Ridge1), AngleGap(theta, Ridge2))) / (Math.PI / 3.0);
            if (m > 1.0) m = 1.0;
            double cone = HeightM - Slope * (0.5 + 0.5 * m) * r;
            double shoulder = HeightM - 1500.0 - 0.09 * r;
            double h = Math.Max(cone, shoulder);
            return h < FootM ? float.MinValue : (float)h;
        }

        private static double AngleGap(double a, double b)
        {
            double d = Math.Abs(a - b) % (2.0 * Math.PI);
            return d > Math.PI ? 2.0 * Math.PI - d : d;
        }
    }

    /// <summary>
    /// The heights the skyline is built from (engine-free, read-only once built, safe to share between threads): one
    /// regular grid resampled from the region pack's horizon tiles (levels 5-7; the finest available wins, coarser
    /// spacings keep every summit by taking the highest sample of each cell) plus the curated summit cones of
    /// <see cref="HorizonPeaks"/>: real summits are raised to their published heights (the GLO-30 horizon tiles hold them
    /// 40-260 m low) and peaks the tiles do not reach get a synthetic massif.
    /// </summary>
    public sealed class HorizonHeightField
    {
        /// <summary>Grid code for "no data" (the quantised -100 m, which never occurs in the region).</summary>
        private const ushort NoData = 0;

        /// <summary>Slope of a real summit's cone (about 39 degrees): it only rises above the DEM within a few hundred
        /// metres of the summit.</summary>
        public const float SummitSlope = 0.8f;

        /// <summary>Slope between the ridges of a synthetic massif (about 31 degrees).</summary>
        public const float MassifSlope = 0.6f;

        private readonly ushort[] _q;
        private readonly HorizonCone[] _cones;

        /// <summary>West and south edge of the grid (game metres) and its spacing.</summary>
        public readonly double X0, Z0, SpacingM;

        /// <summary>Samples per row (west to east) and rows (south to north).</summary>
        public readonly int Nx, Nz;

        /// <summary>A grid of <paramref name="nx"/> × <paramref name="nz"/> samples (row-major, row 0 south; NaN = no data)
        /// with the curated cones placed for it. For tests and tools.</summary>
        public HorizonHeightField(double x0, double z0, double spacingM, int nx, int nz, float[] heights, bool withPeaks = true)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (nx < 2 || nz < 2 || heights.Length < nx * nz) throw new ArgumentException("grid too small");
            if (!(spacingM > 0)) throw new ArgumentOutOfRangeException(nameof(spacingM));
            X0 = x0;
            Z0 = z0;
            SpacingM = spacingM;
            Nx = nx;
            Nz = nz;
            _q = new ushort[nx * nz];
            for (int i = 0; i < nx * nz; i++) _q[i] = float.IsNaN(heights[i]) ? NoData : Math.Max((ushort)1, Ght.Quantize(heights[i]));
            _cones = withPeaks ? PlaceCones() : new HorizonCone[0];
        }

        private HorizonHeightField(double x0, double z0, double spacingM, int nx, int nz, ushort[] q, bool withPeaks)
        {
            X0 = x0;
            Z0 = z0;
            SpacingM = spacingM;
            Nx = nx;
            Nz = nz;
            _q = q;
            _cones = withPeaks ? PlaceCones() : new HorizonCone[0];
        }

        /// <summary>Bytes held by the grid.</summary>
        public long Bytes
        {
            get { return _q.LongLength * 2L; }
        }

        /// <summary>The summit cones (real and synthetic).</summary>
        public int ConeCount
        {
            get { return _cones.Length; }
        }

        public HorizonCone Cone(int i)
        {
            return _cones[i];
        }

        /// <summary>
        /// Builds the field from every tile of <paramref name="pack"/> at level <paramref name="maxLevel"/> or coarser
        /// (the horizon levels), coarse levels first so finer ones overwrite them. Reads and decodes those tiles (a
        /// few hundred milliseconds for the Kathmandu horizon: run it on a worker). Null when the pack has no horizon
        /// heights.
        /// </summary>
        public static HorizonHeightField FromPack(PackReader pack, double spacingM, int maxLevel = 7, bool withPeaks = true)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));
            if (!(spacingM >= 16)) throw new ArgumentOutOfRangeException(nameof(spacingM));
            var tiles = new List<TileId>();
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            foreach (PackEntry e in pack.Entries)
            {
                TileId t = e.Tile;
                if (t.Level > maxLevel) continue;
                tiles.Add(t);
                minX = Math.Min(minX, t.X0);
                minZ = Math.Min(minZ, t.Z0);
                maxX = Math.Max(maxX, t.X0 + t.Size);
                maxZ = Math.Max(maxZ, t.Z0 + t.Size);
            }
            if (tiles.Count == 0) return null;
            // Coarse first: finer tiles overwrite.
            tiles.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Key.CompareTo(b.Key));
            double x0 = Math.Floor(minX / spacingM) * spacingM, z0 = Math.Floor(minZ / spacingM) * spacingM;
            int nx = (int)Math.Floor((maxX - x0) / spacingM) + 1, nz = (int)Math.Floor((maxZ - z0) / spacingM) + 1;
            var q = new ushort[nx * nz];
            bool any = false;
            foreach (TileId id in tiles)
            {
                TileData t = pack.ReadTile(id);
                if (t == null || t.HeightsQ == null || t.HeightsN < 2) continue;
                Stamp(t, q, x0, z0, spacingM, nx, nz);
                any = true;
            }
            return any ? new HorizonHeightField(x0, z0, spacingM, nx, nz, q, withPeaks) : null;
        }

        /// <summary>Writes a tile's heights into the grid samples inside it: bilinear when the tile is no finer than the
        /// grid, else the highest tile sample of the cell [x - s/2, x + s/2).</summary>
        private static void Stamp(TileData t, ushort[] q, double x0, double z0, double s, int nx, int nz)
        {
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1), tx0 = t.Tile.X0, tz0 = t.Tile.Z0;
            int i0 = Math.Max(0, (int)Math.Ceiling((tx0 - x0) / s - 1e-9)), i1 = Math.Min(nx - 1, (int)Math.Floor((tx0 + size - x0) / s + 1e-9));
            int j0 = Math.Max(0, (int)Math.Ceiling((tz0 - z0) / s - 1e-9)), j1 = Math.Min(nz - 1, (int)Math.Floor((tz0 + size - z0) / s + 1e-9));
            bool pool = cell < s * 0.75;
            double half = 0.5 * s / cell;
            for (int j = j0; j <= j1; j++)
            {
                double vz = (z0 + j * s - tz0) / cell;
                for (int i = i0; i <= i1; i++)
                {
                    double vx = (x0 + i * s - tx0) / cell;
                    ushort code;
                    if (!pool)
                    {
                        code = BilinearCode(t, n, vx, vz);
                    }
                    else
                    {
                        // Highest sample in [g - s/2, g + s/2) on both axes.
                        int a0 = Math.Max(0, (int)Math.Ceiling(vx - half - 1e-9)), a1 = Math.Min(n - 1, (int)Math.Ceiling(vx + half - 1e-9) - 1);
                        int b0 = Math.Max(0, (int)Math.Ceiling(vz - half - 1e-9)), b1 = Math.Min(n - 1, (int)Math.Ceiling(vz + half - 1e-9) - 1);
                        if (a1 < a0) a1 = a0;
                        if (b1 < b0) b1 = b0;
                        ushort best = 0;
                        for (int b = b0; b <= b1; b++)
                            for (int a = a0; a <= a1; a++)
                            {
                                ushort c = t.HeightsQ[b * n + a];
                                if (c > best) best = c;
                            }
                        code = best;
                    }
                    q[j * nx + i] = Math.Max((ushort)1, code);
                }
            }
        }

        private static ushort BilinearCode(TileData t, int n, double vx, double vz)
        {
            int a = Math.Max(0, Math.Min(n - 2, (int)Math.Floor(vx))), b = Math.Max(0, Math.Min(n - 2, (int)Math.Floor(vz)));
            double fx = Math.Max(0.0, Math.Min(1.0, vx - a)), fz = Math.Max(0.0, Math.Min(1.0, vz - b));
            double h00 = t.HeightsQ[b * n + a], h10 = t.HeightsQ[b * n + a + 1], h01 = t.HeightsQ[(b + 1) * n + a], h11 = t.HeightsQ[(b + 1) * n + a + 1];
            double h = (h00 * (1 - fx) + h10 * fx) * (1 - fz) + (h01 * (1 - fx) + h11 * fx) * fz;
            return (ushort)Math.Max(0.0, Math.Min(65535.0, Math.Round(h)));
        }

        /// <summary>True when game (x, z) lies on the grid.</summary>
        public bool Covers(double x, double z)
        {
            double u = (x - X0) / SpacingM, v = (z - Z0) / SpacingM;
            return u >= 0 && v >= 0 && u <= Nx - 1 && v <= Nz - 1;
        }

        /// <summary>Bilinear height of the grid alone at game (x, z); false off the grid or next to a sample without
        /// data.</summary>
        public bool TryGridHeight(double x, double z, out float h)
        {
            double u = (x - X0) / SpacingM, v = (z - Z0) / SpacingM;
            h = 0f;
            if (!(u >= 0) || !(v >= 0) || u > Nx - 1 || v > Nz - 1) return false;
            int i = Math.Min(Nx - 2, (int)u), j = Math.Min(Nz - 2, (int)v);
            int k = j * Nx + i;
            ushort a = _q[k], b = _q[k + 1], c = _q[k + Nx], d = _q[k + Nx + 1];
            if (a == NoData || b == NoData || c == NoData || d == NoData) return false;
            double fx = u - i, fz = v - j;
            double code = (a * (1 - fx) + b * fx) * (1 - fz) + (c * (1 - fx) + d * fx) * fz;
            h = (float)(Ght.HeightMinM + code * Ght.HeightStepM);
            return true;
        }

        /// <summary>Highest of the grid and every cone at game (x, z); false when neither has a height there.</summary>
        public bool TryHeight(double x, double z, out float h)
        {
            bool has = TryGridHeight(x, z, out h);
            if (!has) h = float.MinValue;
            for (int i = 0; i < _cones.Length; i++)
            {
                float ch = ConeHeight(i, x, z);
                if (ch > h)
                {
                    h = ch;
                    has = true;
                }
            }
            if (!has) h = 0f;
            return has;
        }

        /// <summary>Height of cone <paramref name="i"/> at game (x, z), or <see cref="float.MinValue"/> beyond its
        /// reach.</summary>
        public float ConeHeight(int i, double x, double z)
        {
            HorizonCone c = _cones[i];
            double dx = x - c.X, dz = z - c.Z;
            double r2 = dx * dx + dz * dz;
            if (r2 >= (double)c.ReachM * c.ReachM) return float.MinValue;
            return c.HeightAt(Math.Sqrt(r2), Math.Atan2(dx, dz));
        }

        /// <summary>Gradient magnitude of the grid at game (x, z) (rise per metre, central differences over one cell);
        /// 0 off the grid. Steep ground sheds snow: the skyline shows rock there.</summary>
        public float Steepness(double x, double z)
        {
            float e, w, n, s;
            double d = SpacingM;
            if (!TryGridHeight(x + d, z, out e) || !TryGridHeight(x - d, z, out w) || !TryGridHeight(x, z + d, out n) ||
                !TryGridHeight(x, z - d, out s)) return 0f;
            double gx = (e - w) / (2 * d), gz = (n - s) / (2 * d);
            return (float)Math.Sqrt(gx * gx + gz * gz);
        }

        /// <summary>
        /// The curated summits: a real summit inside the grid gets a steep cone to its published height (only where it
        /// rises above the DEM); a snow peak outside the grid gets a synthetic massif whose three ridges point in
        /// directions derived from its id (deterministic).
        /// </summary>
        private HorizonCone[] PlaceCones()
        {
            var list = new List<HorizonCone>(HorizonPeaks.Count);
            for (int i = 0; i < HorizonPeaks.Count; i++)
            {
                HorizonPeak p = HorizonPeaks.At(i);
                double x, z;
                p.GamePosition(out x, out z);
                float g;
                bool inside = TryGridHeight(x, z, out g);
                var c = new HorizonCone { X = x, Z = z, HeightM = (float)p.HeightM };
                if (inside)
                {
                    c.Slope = SummitSlope;
                    // Reach where the cone has fallen 900 m: far below the DEM summit, so max() hands back to the DEM.
                    c.ReachM = 900f / SummitSlope;
                }
                else
                {
                    if (p.Kind != HorizonPeakKind.SnowPeak) continue; // rim hills always lie inside the valley's tiles
                    c.Synthetic = true;
                    c.Slope = MassifSlope;
                    c.ReachM = (float)Math.Max((p.HeightM - HorizonCone.FootM) / (MassifSlope * 0.5),
                                               (p.HeightM - 1500.0 - HorizonCone.FootM) / 0.09);
                    byte[] id = System.Text.Encoding.UTF8.GetBytes(p.Id);
                    uint hsh = Hashes.Fnv1a32(id, 0, id.Length);
                    double r0 = (hsh & 0x3FF) / 1024.0 * 2.0 * Math.PI;
                    c.Ridge0 = (float)r0;
                    c.Ridge1 = (float)(r0 + 2.0 * Math.PI / 3.0 + ((hsh >> 10 & 0xFF) / 255.0 - 0.5) * 0.6);
                    c.Ridge2 = (float)(r0 + 4.0 * Math.PI / 3.0 + ((hsh >> 18 & 0xFF) / 255.0 - 0.5) * 0.6);
                }
                list.Add(c);
            }
            return list.ToArray();
        }
    }
}
