using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The palace ground (kds-palace.md §2.1, §2.3): the square, the courts and the palace stand on one level (±0.3 m), but
    /// the region's terrain model is a surface model that dips to 1299 m and climbs to 1306 m over the roofs round the
    /// palace. <see cref="Apply"/> flattens a decoded tile's height grid to <see cref="KdsPalaceSite.LevelM"/> inside the
    /// palace's zones (palace-grid rectangles round its blocks, the gate apron, Gaddi Baithak, Kumari Ghar and the Taleju
    /// precinct) and clamps the terrain round them to a cone of slopes back to the original, so the terrain mesh, the
    /// sampler, the ground query and every placer read the floor the palace is built on. Idempotent, any tile level; the
    /// streamer calls it on every decoded tile before meshing (HeroBuilder.PrepareGround).
    /// </summary>
    public static class KpGround
    {
        /// <summary>Level zones in palace-grid metres: a0, a1, b0, b1.</summary>
        private static readonly double[] Zones =
        {
            -11.6, 59.5, -76.5, 41.5, // the palace east of the gate: Mohan, Nasal, Mul, Lohan, the towers, the south facade
            -16.5, -11.6, -12.0, 13.0, // the gate apron (lions, Hanuman, steps)
            -40.5, -11.6, -76.5, -7.5, // the Dakha and Tribhuvan block with the stepped platform on the forecourt
            -84.0, -40.5, -82.5, -41.0, // Gaddi Baithak with its portico podium, stairs and the Hnulunche range
            -95.5, -69.5, -118.0, -88.5, // Kumari Ghar and its plinth
            8.5, 61.5, 27.5, 83.5, // the Taleju plinth, precinct wall and the north shop row
            56.0, 82.0, -68.5, -55.0, // the east shop range on the street
        };

        /// <summary>Slope (rise over run) of the terrain eased back round the zones, and how far the easing reaches.</summary>
        public const double BlendSlope = 0.4, BlendReachM = 18.0;

        /// <summary>True when palace-grid (a, b) lies in a level zone.</summary>
        public static bool InZone(double a, double b)
        {
            for (int i = 0; i < Zones.Length; i += 4)
                if (a >= Zones[i] && a <= Zones[i + 1] && b >= Zones[i + 2] && b <= Zones[i + 3]) return true;
            return false;
        }

        /// <summary>Distance (m) from palace-grid (a, b) to the nearest level zone (0 inside one).</summary>
        public static double Distance(double a, double b)
        {
            double best = double.MaxValue;
            for (int i = 0; i < Zones.Length; i += 4)
            {
                double da = a < Zones[i] ? Zones[i] - a : a > Zones[i + 1] ? a - Zones[i + 1] : 0;
                double db = b < Zones[i + 2] ? Zones[i + 2] - b : b > Zones[i + 3] ? b - Zones[i + 3] : 0;
                best = Math.Min(best, Math.Sqrt(da * da + db * db));
            }
            return best;
        }

        /// <summary>Flatten the palace in a decoded tile's height grid; returns the samples changed. A tile the site does
        /// not reach is left alone.</summary>
        public static int Apply(TileData t)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            KpFrame f = KpFrame.For(t, 0, null);
            // The zones' tile-local bounding box with the easing reach.
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < Zones.Length; i += 4)
            {
                for (int q = 0; q < 4; q++)
                {
                    double a = (q & 1) == 0 ? Zones[i] - BlendReachM : Zones[i + 1] + BlendReachM;
                    double b = (q & 2) == 0 ? Zones[i + 2] - BlendReachM : Zones[i + 3] + BlendReachM;
                    double x, y, tx, tz;
                    KpFrame.FromGrid(a, b, out x, out y);
                    f.ToTile(x, y, out tx, out tz);
                    minX = Math.Min(minX, tx);
                    maxX = Math.Max(maxX, tx);
                    minZ = Math.Min(minZ, tz);
                    maxZ = Math.Max(maxZ, tz);
                }
            }
            if (maxX < 0 || maxZ < 0 || minX > size || minZ > size) return 0;
            int i0 = Math.Max(0, (int)Math.Floor(minX / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling(maxX / cell));
            int j0 = Math.Max(0, (int)Math.Floor(minZ / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling(maxZ / cell));
            int changed = 0;
            double level = f.Level;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double x, y, a, b;
                    f.FromTile(i * cell, j * cell, out x, out y);
                    KpFrame.ToGrid(x, y, out a, out b);
                    double d = Distance(a, b);
                    if (d > BlendReachM) continue;
                    int k = j * n + i;
                    ushort q = t.HeightsQ[k];
                    double h = Ght.Dequantize(q);
                    if (d <= 0) h = level;
                    else
                    {
                        double band = BlendSlope * d;
                        h = Math.Max(level - band, Math.Min(level + band, h));
                    }
                    ushort nq = Ght.Quantize(h);
                    if (nq == q) continue;
                    t.HeightsQ[k] = nq;
                    changed++;
                }
            }
            return changed;
        }
    }
}
