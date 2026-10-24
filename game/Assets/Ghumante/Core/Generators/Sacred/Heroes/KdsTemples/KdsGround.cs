using System;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The ground of Kathmandu Durbar Square (kds-temples.md §2.1, §2.3; judge round 1): one continuous, nearly level
    /// plaza of grey stone flags with every plinth standing cleanly on it. The region's terrain model is a surface model
    /// that varies 2-3.5 m under single monuments (Kasthamandap 2.0-3.4 m, Basantapur 2.3 m, Maju Dega to Jagannath 3.5 m),
    /// so <see cref="Apply"/> levels a decoded tile's height grid inside the square: flat at <see cref="LevelM"/> (the
    /// palace package's level, so the two halves of the square meet) east of <see cref="RampEastX"/>, rising evenly to
    /// <see cref="LevelM"/> + <see cref="WestRiseM"/> at Maru (the square falls gently to the east, at most 1.5 m), every
    /// structure on its own level pad (its centre's level, with a <see cref="ApronM"/> apron easing into the plaza), and
    /// the terrain round the zones clamped to a cone of slopes back to the original (<see cref="BlendSlope"/>, up to
    /// <see cref="BlendReachM"/>). The zones are the pedestrian square (OSM r21291455, outer w1257513676), the Basantapur
    /// marketplace (w218440319) and the street along its south edge, the temple field, Maru and the lane round Silyan
    /// Sattal, the bell lane, the north field (Makhan Tole side), and the pads. Brief local metres (origin
    /// 27.7045 N, 85.3075 E; x east, y north). Idempotent and deterministic; the streamer calls it on every decoded tile
    /// before meshing (<see cref="HeroBuilder.PrepareGround"/>), and the site's builds read their ground through
    /// <see cref="Prepared"/>, so a build on a raw tile (tests, tools) stands on the same floor as in the streamed world.
    /// </summary>
    public static class KdsGround
    {
        /// <summary>The square's level east of <see cref="RampEastX"/> (m; the palace package's level, kds-palace.md §2.1).</summary>
        public const double LevelM = 1301.0;

        /// <summary>How much higher Maru (west of <see cref="RampWestX"/>) stands than the east of the square (m).</summary>
        public const double WestRiseM = 1.5;

        /// <summary>Brief x (m) where the plaza starts to rise westward, and where it reaches its west level.</summary>
        public const double RampEastX = -100.0, RampWestX = -190.0;

        /// <summary>Width (m) of the apron round each structure's pad over which its level eases into the plaza's.</summary>
        public const double ApronM = 3.0;

        /// <summary>Slope (rise over run) of the terrain eased back round the zones, and how far the easing reaches (m).</summary>
        public const double BlendSlope = 0.4, BlendReachM = 18.0;

        /// <summary>Height (m) of the flag paving over the levelled ground (above the terrain, below the road ribbons' lift).</summary>
        public const double PavingLiftM = 0.03;

        // The pedestrian square (OSM r21291455, outer ring w1257513676) in the brief's frame.
        internal static readonly double[] SquareX =
        {
            18.6, -2.1, -3.9, -9.9, -12.7, -25.5, -28.4, -31.9, -40.3, -53.9, -70.2, -74.2, -77.0, -79.3, -80.1, -81.0, -85.2, -87.8, -88.7, -93.4, -99.5,
            -107.5, -108.0, -115.0, -116.1, -105.7, -110.4, -89.9, -93.4, -86.2, -83.7, -82.9, -78.1, -4.4, -3.7, -31.5, -38.9, -39.1, -40.2, 7.8, 7.9, 7.4,
            7.0, 22.8, 31.5, 56.8, 68.4, 68.1, 149.3, 148.2, 147.4, 146.7, 146.4, 146.2, 146.4, 146.7, 147.4, 142.2, 129.7, 65.7, 46.0, 45.1, -9.7, -10.3,
            -40.9, -41.9, -94.2, -89.9, -88.4, -85.3, -90.2, -72.6, -69.5, -64.9, -64.1, -53.5, -53.5, -37.9, -38.8, -14.1, -3.0, 16.1, 21.7, 15.9,
        };

        internal static readonly double[] SquareY =
        {
            76.8, 81.5, 73.9, 65.4, 63.5, 62.6, 54.0, 50.8, 44.2, 28.5, 10.1, 10.6, 3.7, 4.6, 3.0, 1.3, -6.4, -5.0, -6.5, -18.0, -24.9, -19.5, -21.2,
            -45.4, -49.3, -51.5, -69.7, -74.0, -94.5, -95.9, -81.4, -76.8, -77.7, -92.9, -115.8, -111.6, -110.4, -111.2, -116.3, -124.5, -117.0, -102.5,
            -88.7, -88.2, -88.6, -87.2, -89.9, -92.7, -103.0, -101.4, -99.7, -97.9, -96.0, -94.1, -92.2, -90.3, -88.5, -86.1, -85.0, -79.5, -77.8, -83.3,
            -75.2, -78.2, -71.7, -74.8, -64.4, -44.9, -45.1, -29.0, -25.5, -1.4, 2.9, 1.4, 4.9, 2.7, 5.4, 2.5, -2.1, -6.2, 42.8, 39.7, 61.8, 63.2,
        };

        // The open ground round the pedestrian square (aerial_west_osm.jpg, the brief's site plan): the temple field
        // round Maju Dega, Trailokya Mohan, the Dabali, the Narayan ruin and Shiva-Parvati; Maru round Kasthamandap,
        // Ashok Binayak, Laxmi Narayan Sattal and Kavindrapur, and on south round Silyan Sattal to the shop fronts; the bell
        // lane past the Taleju Bell, Saraswati, Chyasin Dega and the Big Drums; the street along the Basantapur platform's
        // south edge to the shop fronts (c_basantapur_chowk 08-10: flags, not earth; fix round, the tower view V2); the
        // north field from Kotilingeshwar, Mahavishnu, Kashinath and Nila Varahi east past the Shiva Mandiras and the
        // Gorakhnath pipal (short of the Taleju plinth, whose ground the palace keeps) up to Makhan Tole's street
        // (KAT1530, KAT1550, KAT1551: paved to the walls).
        private static readonly double[][] OpenX =
        {
            new[] { -146.0, -100.0, -92.0, -92.0, -86.0, -86.0, -132.0, -146.0 },
            new[] { -197.0, -160.0, -152.0, -146.0, -146.0, -160.0, -190.0, -197.0 },
            new[] { -86.0, -72.0, -54.0, -50.0, -68.0, -90.0 },
            new[] { -86.2, -3.7, -5.6, -88.1 },
            new[] { -26.0, -20.0, -4.0, 24.0, 21.0, 18.0, 4.0, -24.0 },
        };

        private static readonly double[][] OpenY =
        {
            new[] { -72.0, -72.0, -62.0, -30.0, -28.0, 6.0, 8.0, -12.0 },
            new[] { -104.0, -106.0, -100.0, -74.0, -40.0, -36.0, -38.0, -52.0 },
            new[] { -6.0, 6.0, 24.0, 44.0, 46.0, 6.0 },
            new[] { -95.9, -115.8, -123.6, -103.7 },
            new[] { 78.0, 90.0, 86.5, 79.0, 70.0, 36.0, 33.0, 52.0 },
        };

        /// <summary>The plaza's level (m) at brief x, before quantisation: flat east, rising evenly to Maru.</summary>
        public static double Ramp(double bx)
        {
            double t = (RampEastX - bx) / (RampEastX - RampWestX);
            return LevelM + WestRiseM * (t < 0 ? 0 : t > 1 ? 1 : t);
        }

        /// <summary>A height as the terrain grid stores it (0.15 m codes).</summary>
        public static double Snap(double h)
        {
            return Ght.Dequantize(Ght.Quantize(h));
        }

        /// <summary>The level (m) a structure of the site stands on: its pad's, the plaza's level at its brief position.</summary>
        internal static double PadLevel(double bx)
        {
            return Snap(Ramp(bx));
        }

        /// <summary>True when brief (x, y) lies inside the polygon (xs, ys).</summary>
        internal static bool Inside(double[] xs, double[] ys, double x, double y)
        {
            int n = xs.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance (m) from brief (x, y) to the polygon's boundary (0 inside).</summary>
        internal static double Distance(double[] xs, double[] ys, double x, double y)
        {
            if (Inside(xs, ys, x, y)) return 0;
            int n = xs.Length;
            double best = double.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double dx = xs[i] - xs[j], dy = ys[i] - ys[j], l2 = dx * dx + dy * dy;
                double t = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((x - xs[j]) * dx + (y - ys[j]) * dy) / l2)) : 0;
                double ex = xs[j] + t * dx - x, ey = ys[j] + t * dy - y;
                best = Math.Min(best, ex * ex + ey * ey);
            }
            return Math.Sqrt(best);
        }

        /// <summary>True when brief (x, y) is in the paved open ground of the square (the pedestrian square, the temple
        /// field, Maru or the bell lane; not the Basantapur platform, which the square's own hero paves in brick).</summary>
        public static bool InPlaza(double bx, double by)
        {
            if (Inside(SquareX, SquareY, bx, by)) return true;
            for (int i = 0; i < OpenX.Length; i++)
                if (Inside(OpenX[i], OpenY[i], bx, by)) return true;
            return false;
        }

        /// <summary>
        /// The level (m) the ground is set to at brief (x, y), NaN outside every zone: a structure's pad level inside its pad,
        /// eased over its apron into the plaza's level, the plaza's (quantised) level in the open zones and on the
        /// Basantapur platform.
        /// </summary>
        public static double LevelAt(double bx, double by)
        {
            double ramp = Snap(Ramp(bx));
            double padD;
            double pad = NearestPad(bx, by, out padD);
            if (!double.IsNaN(pad))
            {
                if (padD <= 0) return pad;
                if (padD < ApronM) return pad + (ramp - pad) * padD / ApronM;
            }
            if (InPlaza(bx, by) || Inside(Basantapur.MarketX, Basantapur.MarketY, bx, by)) return ramp;
            return double.NaN;
        }

        /// <summary>The level of the pad nearest brief (x, y) (by the distance past its radius, <paramref name="d"/>), NaN
        /// when no pad's apron reaches the point.</summary>
        private static double NearestPad(double bx, double by, out double d)
        {
            d = double.MaxValue;
            double level = double.NaN;
            foreach (KdsTemplesSite.Entry e in KdsTemplesSite.Entries)
            {
                if (e.PadR <= 0) continue;
                double dx = bx - e.X, dy = by - e.Y, r = Math.Sqrt(dx * dx + dy * dy) - e.PadR;
                if (r >= ApronM || r >= d) continue;
                d = r;
                level = PadLevel(e.X);
            }
            return level;
        }

        /// <summary>Distance (m) from brief (x, y) to the nearest zone (0 inside one).</summary>
        public static double ZoneDistance(double bx, double by)
        {
            double best = Distance(SquareX, SquareY, bx, by);
            for (int i = 0; i < OpenX.Length && best > 0; i++) best = Math.Min(best, Distance(OpenX[i], OpenY[i], bx, by));
            if (best > 0) best = Math.Min(best, Distance(Basantapur.MarketX, Basantapur.MarketY, bx, by));
            foreach (KdsTemplesSite.Entry e in KdsTemplesSite.Entries)
            {
                if (e.PadR <= 0 || best <= 0) continue;
                double dx = bx - e.X, dy = by - e.Y;
                best = Math.Min(best, Math.Max(0, Math.Sqrt(dx * dx + dy * dy) - e.PadR - ApronM));
            }
            return best;
        }

        /// <summary>Tile-local metres to the brief's frame (an affine fit of the projection over the tile, exact to
        /// millimetres across a level-10 tile).</summary>
        internal struct BriefMap
        {
            public double Ox, Oy, Xx, Xy, Zx, Zy;

            public static BriefMap For(TileData t)
            {
                double ox, oy, ax, ay, bx, by;
                ToBrief(t, 0, 0, out ox, out oy);
                ToBrief(t, 1000, 0, out ax, out ay);
                ToBrief(t, 0, 1000, out bx, out by);
                return new BriefMap { Ox = ox, Oy = oy, Xx = (ax - ox) / 1000, Xy = (ay - oy) / 1000, Zx = (bx - ox) / 1000, Zy = (by - oy) / 1000 };
            }

            public void Map(double tx, double tz, out double bx, out double by)
            {
                bx = Ox + Xx * tx + Zx * tz;
                by = Oy + Xy * tx + Zy * tz;
            }
        }

        /// <summary>Tile-local metres to the brief's frame (exact).</summary>
        internal static void ToBrief(TileData t, double tx, double tz, out double bx, out double by)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(t.Tile.X0 + tx, t.Tile.Z0 + tz, out lon, out lat);
            by = (lat - KdsTemplesSite.OriginLat) * KdsTemplesSite.MPerDegLat;
            bx = (lon - KdsTemplesSite.OriginLon) * KdsTemplesSite.MPerDegLon;
        }

        /// <summary>Level the square in a decoded tile's height grid (see the class summary); returns the samples changed.
        /// A tile the square does not reach is left alone.</summary>
        public static int Apply(TileData t)
        {
            return Flatten(t, true);
        }

        private static readonly ConditionalWeakTable<TileData, TileData> PreparedCache = new ConditionalWeakTable<TileData, TileData>();

        /// <summary>
        /// The tile's heights as the streamer prepares them, without touching <paramref name="t"/>: the tile itself when it is
        /// already levelled (or the square does not reach it, or it has no heights), else a cached copy with its own
        /// levelled height grid (and nothing else: the site reads its roads, buildings and areas from the tile itself).
        /// </summary>
        public static TileData Prepared(TileData t)
        {
            if (t == null) return null;
            return PreparedCache.GetValue(t, k =>
            {
                if (Flatten(k, false) == 0) return k;
                var copy = new TileData { Tile = k.Tile, HeightsN = k.HeightsN, HeightsQ = (ushort[])k.HeightsQ.Clone() };
                Flatten(copy, true);
                return copy;
            });
        }

        private static readonly ConditionalWeakTable<TileData, TileHeightSampler> SamplerCache = new ConditionalWeakTable<TileData, TileHeightSampler>();

        /// <summary>The full-resolution sampler of the tile's levelled heights (<see cref="Prepared"/>), shared by every build
        /// on the tile (so the drawn road surface it carries is built once); null without heights.</summary>
        public static TileHeightSampler Sampler(TileData t)
        {
            TileData p = Prepared(t);
            if (p == null || p.HeightsQ == null) return null;
            return SamplerCache.GetValue(p, k => new TileHeightSampler(k));
        }

        /// <summary>The samples <see cref="Apply"/> changes; written only when <paramref name="write"/>.</summary>
        private static int Flatten(TileData t, bool write)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2 || t.HeightsQ.Length < t.HeightsN * t.HeightsN) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            // The zones' brief bounding box with the easing reach, mapped to the tile.
            const double x0 = -215.0, x1 = 170.0, y0 = -145.0, y1 = 115.0;
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int q = 0; q < 4; q++)
            {
                double tx, tz;
                KdsTemplesSite.LocalToTile(t, (q & 1) == 0 ? x0 - BlendReachM : x1 + BlendReachM, (q & 2) == 0 ? y0 - BlendReachM : y1 + BlendReachM, out tx,
                                           out tz);
                minX = Math.Min(minX, tx);
                maxX = Math.Max(maxX, tx);
                minZ = Math.Min(minZ, tz);
                maxZ = Math.Max(maxZ, tz);
            }
            if (maxX < 0 || maxZ < 0 || minX > size || minZ > size) return 0;
            int i0 = Math.Max(0, (int)Math.Floor(minX / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling(maxX / cell));
            int j0 = Math.Max(0, (int)Math.Floor(minZ / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling(maxZ / cell));
            BriefMap map = BriefMap.For(t);
            int changed = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double bx, by;
                    map.Map(i * cell, j * cell, out bx, out by);
                    int k = j * n + i;
                    ushort q = t.HeightsQ[k];
                    double h = Ght.Dequantize(q);
                    double level = LevelAt(bx, by);
                    if (!double.IsNaN(level))
                    {
                        h = level;
                    }
                    else
                    {
                        double d = ZoneDistance(bx, by);
                        if (d > BlendReachM) continue;
                        double ramp = Ramp(bx), band = BlendSlope * d;
                        h = Math.Max(ramp - band, Math.Min(ramp + band, h));
                    }
                    ushort nq = Ght.Quantize(h);
                    if (nq == q) continue;
                    if (write) t.HeightsQ[k] = nq;
                    changed++;
                }
            }
            return changed;
        }
    }

    /// <summary>
    /// The road classes a monument's eave may hang over at the lower foot clearance (judge round 1): footways, paths,
    /// steps, cycleways and pedestrian streets carry people, bicycles and motorbikes, never a bus, so an eave over their
    /// corridor keeps <see cref="FootClearanceM"/> (a rider and a margin); every other road keeps the contract's
    /// <see cref="RoadClearance.MinOverheadClearanceM"/>. The vehicle roads' corridors as the sacred generators' own
    /// stand-in draws them (<see cref="SacredRoads"/>: as wide as the widest drawn game width, never narrower than
    /// <see cref="RoadClearance.MinCorridorM"/>). Tile-local metres; cached per tile; immutable.
    /// </summary>
    internal sealed class KdsVehicleRoads
    {
        /// <summary>Overhead clearance (m) over a foot-only corridor.</summary>
        public const double FootClearanceM = 2.6;

        private static readonly ConditionalWeakTable<TileData, KdsVehicleRoads> Cache = new ConditionalWeakTable<TileData, KdsVehicleRoads>();
        private readonly double[] _ax, _az, _bx, _bz, _half;

        public static KdsVehicleRoads For(TileData t)
        {
            return Cache.GetValue(t, k => new KdsVehicleRoads(k));
        }

        /// <summary>True for the classes that carry people, bicycles and motorbikes only.</summary>
        public static bool FootOnly(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Cycleway || c == RoadClass.Bridleway ||
                   c == RoadClass.Pedestrian;
        }

        private KdsVehicleRoads(TileData t)
        {
            int segs = 0;
            for (int i = 0; i < t.Roads.Count; i++)
                if (Counts(t.Roads[i])) segs += t.Roads[i].PointCount - 1;
            _ax = new double[segs];
            _az = new double[segs];
            _bx = new double[segs];
            _bz = new double[segs];
            _half = new double[segs];
            int s = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!Counts(r)) continue;
                double half = 0.5 * Math.Max(RoadClearance.MinCorridorM, RoadWidthModel.MaxGameWidthM(r, t.RoadAttrOf(i)));
                int[] p = r.Points;
                for (int k = 0; k + 1 < p.Length / 2; k++)
                {
                    _ax[s] = p[2 * k] / 100.0;
                    _az[s] = p[2 * k + 1] / 100.0;
                    _bx[s] = p[2 * k + 2] / 100.0;
                    _bz[s] = p[2 * k + 3] / 100.0;
                    _half[s] = half;
                    s++;
                }
            }
        }

        private static bool Counts(RoadRecord r)
        {
            return (r.Flags & RoadFlags.Tunnel) == 0 && r.Layer >= 0 && r.PointCount >= 2 && r.RoadClass != RoadClass.Unknown && !FootOnly(r.RoadClass);
        }

        /// <summary>Signed distance (m) from a tile-local point to the nearest vehicle road corridor (negative inside).</summary>
        public double Distance(double x, double z)
        {
            double best = double.MaxValue;
            for (int k = 0; k < _ax.Length; k++)
            {
                double dx = _bx[k] - _ax[k], dz = _bz[k] - _az[k];
                if (Math.Min(_ax[k], _bx[k]) - _half[k] - 10 > x || Math.Max(_ax[k], _bx[k]) + _half[k] + 10 < x) continue;
                if (Math.Min(_az[k], _bz[k]) - _half[k] - 10 > z || Math.Max(_az[k], _bz[k]) + _half[k] + 10 < z) continue;
                double l2 = dx * dx + dz * dz;
                double f = l2 > 0 ? Math.Max(0, Math.Min(1, ((x - _ax[k]) * dx + (z - _az[k]) * dz) / l2)) : 0;
                double qx = _ax[k] + dx * f - x, qz = _az[k] + dz * f - z;
                best = Math.Min(best, Math.Sqrt(qx * qx + qz * qz) - _half[k]);
            }
            return best;
        }
    }
}
