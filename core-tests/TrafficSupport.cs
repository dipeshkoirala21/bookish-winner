using System;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Tests
{
    /// <summary>The sample region's lane graph, sacred zones, curated places and transit routes, built once per test run
    /// (W2_DESIGN 10.6: V5, V6, V9 run headless on <c>kathmandu_core</c>).</summary>
    internal static class TrafficData
    {
        private static LaneGraph _graph;
        private static SacredZoneIndex _zones;
        private static RouteSet _routes;
        private static readonly object Gate = new object();

        /// <summary>Points of interest (lon, lat).</summary>
        public static readonly double[] RingRoadChabahil = { 85.3462, 27.7120 };
        public static readonly double[] Boudha = { 85.3620, 27.7214 };
        public static readonly double[] Swayambhu = { 85.2904, 27.7149 };
        public static readonly double[] Basantapur = { 85.3070, 27.7040 };
        public static readonly double[] Pashupati = { 85.3486, 27.7105 };

        public static void Game(double[] lonLat, out double x, out double z)
        {
            WorldFrame.LonLatToGame(lonLat[0], lonLat[1], out x, out z);
        }

        public static SacredZoneIndex Zones
        {
            get
            {
                Build();
                return _zones;
            }
        }

        public static LaneGraph Graph
        {
            get
            {
                Build();
                return _graph;
            }
        }

        public static RouteSet Routes
        {
            get
            {
                Build();
                return _routes;
            }
        }

        private static void Build()
        {
            lock (Gate)
            {
                if (_graph != null) return;
                var db = CuratedDb.Read(File.ReadAllBytes(DrivingData.SamplePath(".curated.ghcd")));
                var zones = new SacredZoneIndex();
                var g = new LaneGraph { Sacred = zones };
                foreach (TileData t in DrivingData.SampleTiles().Values)
                    if (t.Tile.Level == 10) zones.AddTile(t.Tile, t, db);
                foreach (TileData t in DrivingData.SampleTiles().Values)
                    if (t.Tile.Level == 10) g.AddTile(t.Tile, t);
                _routes = RouteSet.Read(File.ReadAllBytes(DrivingData.SamplePath(".transit.ghrt")));
                _zones = zones;
                _graph = g;
            }
        }

        /// <summary>A pedestrian simulation over every leaf tile of the sample.</summary>
        public static PedestrianSim Pedestrians(PedestrianSettings s, ulong seed, TrafficSim traffic)
        {
            var p = new PedestrianSim(Graph, s, seed) { Sacred = Zones, Traffic = traffic };
            foreach (TileData t in DrivingData.SampleTiles().Values)
                if (t.Tile.Level == 10) p.AddTile(t.Tile, t);
            return p;
        }

        /// <summary>Overlapping body pairs (oriented rectangles shrunk by <paramref name="shrinkM"/>), ghosts excluded.</summary>
        public static int Overlaps(BodyBox[] b, int n, float shrinkM)
        {
            int c = 0;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (b[i].Ghost || b[j].Ghost) continue;
                double dx = b[i].X - b[j].X, dz = b[i].Z - b[j].Z;
                if (dx * dx + dz * dz > 400) continue;
                if (Obb(b[i], b[j], shrinkM)) c++;
            }
            return c;
        }

        /// <summary>Separating-axis test of two bodies seen from above.</summary>
        public static bool Obb(BodyBox a, BodyBox b, float shrink)
        {
            Span<double> ca = stackalloc double[8], cb = stackalloc double[8];
            Corners(a, shrink, ca);
            Corners(b, shrink, cb);
            for (int k = 0; k < 4; k++)
            {
                BodyBox box = k < 2 ? a : b;
                double fx = Math.Sin(box.HeadingRad), fz = Math.Cos(box.HeadingRad);
                double ax = (k & 1) == 0 ? fx : fz, az = (k & 1) == 0 ? fz : -fx;
                double amin = double.MaxValue, amax = double.MinValue, bmin = double.MaxValue, bmax = double.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    double pa = ca[2 * i] * ax + ca[2 * i + 1] * az, pb = cb[2 * i] * ax + cb[2 * i + 1] * az;
                    amin = Math.Min(amin, pa);
                    amax = Math.Max(amax, pa);
                    bmin = Math.Min(bmin, pb);
                    bmax = Math.Max(bmax, pb);
                }
                if (amax < bmin || bmax < amin) return false;
            }
            return true;
        }

        private static void Corners(BodyBox b, float shrink, Span<double> c)
        {
            double fx = Math.Sin(b.HeadingRad), fz = Math.Cos(b.HeadingRad), rx = fz, rz = -fx;
            double back = b.Back - shrink, ahead = b.Ahead - shrink, h = b.Half - shrink;
            double[] ls = { -back, -back, ahead, ahead }, ws = { -h, h, h, -h };
            for (int k = 0; k < 4; k++)
            {
                c[2 * k] = b.X + fx * ls[k] + rx * ws[k];
                c[2 * k + 1] = b.Z + fz * ls[k] + rz * ws[k];
            }
        }

        /// <summary>A body against a circle (an animal's collider).</summary>
        public static bool CircleHits(BodyBox b, double cx, double cz, double r)
        {
            double fx = Math.Sin(b.HeadingRad), fz = Math.Cos(b.HeadingRad);
            double dx = cx - b.X, dz = cz - b.Z;
            double along = dx * fx + dz * fz, lat = dx * fz - dz * fx;
            double ca = Math.Max(-b.Back, Math.Min(b.Ahead, along)), cl = Math.Max(-b.Half, Math.Min(b.Half, lat));
            double ex = along - ca, el = lat - cl;
            return ex * ex + el * el < r * r;
        }
    }
}
