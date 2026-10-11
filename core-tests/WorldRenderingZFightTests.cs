using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghumante.Core.Meshing;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #4 and #12 ("buildings constantly flicker... make them very solid"): no two front faces of a drawn
    /// layer lie in the same plane and overlap, which the depth buffer cannot order (z-fighting: the two surfaces flicker
    /// through each other as the camera moves). Scans the meshes the game draws at the owner's spots: the near-ring and
    /// lite-ring B0 houses, the B1 band, and the tile's roads, road markings and areas.
    /// </summary>
    public class WorldRenderingZFightTests
    {
        /// <summary>Surfaces closer than this along their shared normal fight on a 24-bit depth buffer from about 60 m
        /// (d² / (near · 2^24) with the 0.4 m near plane) and on any buffer when exactly coplanar.</summary>
        public const float FightM = 0.003f;

        /// <summary>Overlaps smaller than this (m²) are below a few pixels even up close.</summary>
        public const float MinOverlapM2 = 0.0004f;

        [Test]
        public void HousesHaveNoCoplanarOverlappingFaces()
        {
            var report = new StringBuilder();
            int worst = 0;
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                CoplanarScan near = CoplanarScan.Run(scene.Houses, FightM, MinOverlapM2);
                report.Append(spot.Name).Append(" B0 near ring: ").Append(near).Append('\n');
                worst = Math.Max(worst, near.Pairs);
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("zfight_houses.txt", report.ToString());
            Assert.That(worst, Is.EqualTo(0), "coplanar overlapping house faces (z-fighting):\n" + report);
        }
    }

    /// <summary>The ground layers at the owner's spots, as the streamer draws them (a report for the roads and nature
    /// packages: each layer is one material, so a fight inside a layer cannot be ordered by the layer pulls).</summary>
    public class WorldRenderingZFightGroundReport
    {
        [Test]
        public void ReportGroundLayerCoplanarOverlaps()
        {
            var report = new StringBuilder();
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                foreach (int layer in new[] { TileLayers.Roads, TileLayers.RoadDecals, TileLayers.Areas, TileLayers.Bridges })
                {
                    MeshData m = scene.ContextLayer(layer, 150f);
                    CoplanarScan scan = CoplanarScan.Run(m, WorldRenderingZFightTests.FightM, WorldRenderingZFightTests.MinOverlapM2, 4);
                    report.Append(spot.Name).Append(" layer ").Append(layer).Append(": ").Append(scan).Append('\n');
                }
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("zfight_ground.txt", report.ToString());
            Assert.Pass(report.ToString());
        }
    }

    /// <summary>
    /// Finds pairs of triangles of one mesh that face the same way, lie within a distance of each other along their
    /// normal and overlap in that plane (by more than a minimum area): the surfaces a depth buffer cannot order.
    /// Triangles are bucketed by quantised normal and plane offset; candidates in a bucket and its neighbours are clipped
    /// against each other in the plane (Sutherland-Hodgman).
    /// </summary>
    internal sealed class CoplanarScan
    {
        public int Triangles;

        /// <summary>Overlapping pairs whose surfaces look different (colour, material channel or baked AO): the fight
        /// shows as flicker.</summary>
        public int Pairs;

        public double AreaM2;

        /// <summary>... of which exactly coplanar (|Δd| &lt; 0.2 mm).</summary>
        public int Exact;

        /// <summary>Overlapping pairs that shade the same (identical colour, channel and AO): the fight is invisible.</summary>
        public int SamePairs;

        public double SameAreaM2;
        public readonly List<string> Examples = new List<string>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Triangles).Append(" triangles, ").Append(Pairs).Append(" visibly different coplanar overlapping pairs (").Append(Exact)
              .Append(" exactly coplanar), ").Append(AreaM2.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m²; ").Append(SamePairs)
              .Append(" identical-looking pairs, ").Append(SameAreaM2.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m²");
            foreach (string e in Examples) sb.Append("\n    ").Append(e);
            return sb.ToString();
        }

        /// <summary>Called for every visibly different pair: triangle indices and overlap area (diagnostics).</summary>
        public Action<int, int, double> OnPair;

        public static CoplanarScan Run(MeshData m, float fightM, float minOverlapM2, int maxExamples = 12, Action<int, int, double> onPair = null)
        {
            var scan = new CoplanarScan { OnPair = onPair };
            int tris = m.IndexCount / 3;
            scan.Triangles = tris;
            var nx = new float[tris];
            var ny = new float[tris];
            var nz = new float[tris];
            var d = new float[tris];
            var area = new float[tris];
            var buckets = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                float ax = m.Positions[3 * a], ay = m.Positions[3 * a + 1], az = m.Positions[3 * a + 2];
                float e1x = m.Positions[3 * b] - ax, e1y = m.Positions[3 * b + 1] - ay, e1z = m.Positions[3 * b + 2] - az;
                float e2x = m.Positions[3 * c] - ax, e2y = m.Positions[3 * c + 1] - ay, e2z = m.Positions[3 * c + 2] - az;
                float qx = e1y * e2z - e1z * e2y, qy = e1z * e2x - e1x * e2z, qz = e1x * e2y - e1y * e2x;
                float l = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz);
                area[t] = 0.5f * l;
                if (area[t] < minOverlapM2) continue;
                nx[t] = qx / l;
                ny[t] = qy / l;
                nz[t] = qz / l;
                d[t] = nx[t] * ax + ny[t] * ay + nz[t] * az;
                long key = Key(nx[t], ny[t], nz[t], d[t], fightM, 0);
                List<int> list;
                if (!buckets.TryGetValue(key, out list)) buckets[key] = list = new List<int>();
                list.Add(t);
            }
            var pa = new double[16 * 2];
            var pb = new double[16 * 2];
            foreach (KeyValuePair<long, List<int>> kv in buckets)
            {
                List<int> list = kv.Value;
                // Pairs within the bucket, and with the next plane-offset bucket (a pair straddling the rounding edge).
                for (int i = 0; i < list.Count; i++)
                {
                    int t = list[i];
                    for (int j = i + 1; j < list.Count; j++) scan.Test(m, t, list[j], nx, ny, nz, d, fightM, minOverlapM2, maxExamples, pa, pb);
                    List<int> next;
                    if (buckets.TryGetValue(Key(nx[t], ny[t], nz[t], d[t], fightM, 1), out next) && next != list)
                        for (int j = 0; j < next.Count; j++) scan.Test(m, t, next[j], nx, ny, nz, d, fightM, minOverlapM2, maxExamples, pa, pb);
                }
            }
            return scan;
        }

        private static long Key(float nx, float ny, float nz, float d, float fightM, int offset)
        {
            long qx = (long)Math.Round(nx * 100.0) + 128, qy = (long)Math.Round(ny * 100.0) + 128, qz = (long)Math.Round(nz * 100.0) + 128;
            long qd = (long)Math.Floor(d / (2.0 * fightM)) + offset + (1L << 30);
            return (((qx * 256 + qy) * 256 + qz) << 32) ^ qd;
        }

        private void Test(MeshData m, int t, int u, float[] nx, float[] ny, float[] nz, float[] d, float fightM, float minOverlapM2, int maxExamples,
                          double[] pa, double[] pb)
        {
            float dot = nx[t] * nx[u] + ny[t] * ny[u] + nz[t] * nz[u];
            if (dot < 0.9995f) return;
            float gap = Math.Abs(d[t] - d[u]);
            if (gap > fightM) return;
            // Project both into the plane of t.
            float ux, uy, uz;
            if (Math.Abs(nx[t]) < 0.9f)
            {
                ux = 0f;
                uy = nz[t];
                uz = -ny[t];
            }
            else
            {
                ux = -nz[t];
                uy = 0f;
                uz = nx[t];
            }
            float ul = (float)Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            float vx = ny[t] * uz - nz[t] * uy, vy = nz[t] * ux - nx[t] * uz, vz = nx[t] * uy - ny[t] * ux;
            for (int k = 0; k < 3; k++)
            {
                int a = m.Indices[3 * t + k], b = m.Indices[3 * u + k];
                pa[2 * k] = m.Positions[3 * a] * ux + m.Positions[3 * a + 1] * uy + m.Positions[3 * a + 2] * uz;
                pa[2 * k + 1] = m.Positions[3 * a] * vx + m.Positions[3 * a + 1] * vy + m.Positions[3 * a + 2] * vz;
                pb[2 * k] = m.Positions[3 * b] * ux + m.Positions[3 * b + 1] * uy + m.Positions[3 * b + 2] * uz;
                pb[2 * k + 1] = m.Positions[3 * b] * vx + m.Positions[3 * b + 1] * vy + m.Positions[3 * b + 2] * vz;
            }
            // Quick reject on bounds.
            if (Math.Max(pa[0], Math.Max(pa[2], pa[4])) <= Math.Min(pb[0], Math.Min(pb[2], pb[4])) ||
                Math.Max(pb[0], Math.Max(pb[2], pb[4])) <= Math.Min(pa[0], Math.Min(pa[2], pa[4])) ||
                Math.Max(pa[1], Math.Max(pa[3], pa[5])) <= Math.Min(pb[1], Math.Min(pb[3], pb[5])) ||
                Math.Max(pb[1], Math.Max(pb[3], pb[5])) <= Math.Min(pa[1], Math.Min(pa[3], pa[5])))
                return;
            double overlap = ClipArea(pa, pb);
            if (overlap < minOverlapM2) return;
            if (LookAlike(m, t, u))
            {
                SamePairs++;
                SameAreaM2 += overlap;
                return;
            }
            Pairs++;
            AreaM2 += overlap;
            if (gap < 0.0002f) Exact++;
            OnPair?.Invoke(t, u, overlap);
            if (Examples.Count < maxExamples)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * u];
                string ch = m.HasUv0
                    ? " channels " + (int)Math.Round(m.Uv0[2 * a]) + "/" + (int)Math.Round(m.Uv0[2 * b])
                    : "";
                Examples.Add(string.Format(CultureInfo.InvariantCulture, "at ({0:0.00}, {1:0.00}, {2:0.00}) n ({3:0.00}, {4:0.00}, {5:0.00}) gap {6:0.0000} m overlap {7:0.0000} m²{8} colours #{9:X2}{10:X2}{11:X2}/#{12:X2}{13:X2}{14:X2}",
                                           m.Positions[3 * a], m.Positions[3 * a + 1], m.Positions[3 * a + 2], nx[t], ny[t], nz[t], gap, overlap, ch,
                                           m.Colors[4 * a], m.Colors[4 * a + 1], m.Colors[4 * a + 2], m.Colors[4 * b], m.Colors[4 * b + 1], m.Colors[4 * b + 2]));
            }
        }

        /// <summary>Both triangles shade alike: every vertex colour within 6/255, the same material channel and AO within
        /// 0.06 (averaged over the corners).</summary>
        private static bool LookAlike(MeshData m, int t, int u)
        {
            float[] ca = new float[6], cb = new float[6];
            for (int k = 0; k < 3; k++)
            {
                int a = m.Indices[3 * t + k], b = m.Indices[3 * u + k];
                for (int c = 0; c < 3; c++)
                {
                    ca[c] += m.Colors[4 * a + c] / 3f;
                    cb[c] += m.Colors[4 * b + c] / 3f;
                }
                if (m.HasUv0)
                {
                    ca[3] += m.Uv0[2 * a] / 3f;
                    cb[3] += m.Uv0[2 * b] / 3f;
                    ca[4] += m.Uv0[2 * a + 1] / 3f;
                    cb[4] += m.Uv0[2 * b + 1] / 3f;
                }
            }
            for (int c = 0; c < 3; c++)
                if (Math.Abs(ca[c] - cb[c]) > 6f) return false;
            if (Math.Abs(ca[3] - cb[3]) > 0.25f) return false;
            return Math.Abs(ca[4] - cb[4]) <= 0.06f;
        }

        private readonly List<double> _poly = new List<double>(32), _next = new List<double>(32);

        /// <summary>Area of the intersection of two 2D triangles (any winding).</summary>
        private double ClipArea(double[] a, double[] b)
        {
            _poly.Clear();
            for (int k = 0; k < 6; k++) _poly.Add(a[k]);
            double sb = Signed(b);
            if (Math.Abs(sb) < 1e-12) return 0;
            for (int e = 0; e < 3 && _poly.Count >= 6; e++)
            {
                double x0 = b[2 * e], y0 = b[2 * e + 1], x1 = b[2 * ((e + 1) % 3)], y1 = b[2 * ((e + 1) % 3) + 1];
                _next.Clear();
                int n = _poly.Count / 2;
                for (int i = 0; i < n; i++)
                {
                    double px = _poly[2 * i], py = _poly[2 * i + 1], qx = _poly[2 * ((i + 1) % n)], qy = _poly[2 * ((i + 1) % n) + 1];
                    double sp = Side(x0, y0, x1, y1, px, py) * Math.Sign(sb), sq = Side(x0, y0, x1, y1, qx, qy) * Math.Sign(sb);
                    if (sp >= 0)
                    {
                        _next.Add(px);
                        _next.Add(py);
                    }
                    if ((sp >= 0) != (sq >= 0))
                    {
                        double t = sp / (sp - sq);
                        _next.Add(px + (qx - px) * t);
                        _next.Add(py + (qy - py) * t);
                    }
                }
                _poly.Clear();
                _poly.AddRange(_next);
            }
            double area = 0;
            int m = _poly.Count / 2;
            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                area += _poly[2 * i] * _poly[2 * j + 1] - _poly[2 * j] * _poly[2 * i + 1];
            }
            return Math.Abs(area) * 0.5;
        }

        private static double Signed(double[] t)
        {
            return 0.5 * ((t[2] - t[0]) * (t[5] - t[1]) - (t[4] - t[0]) * (t[3] - t[1]));
        }

        private static double Side(double x0, double y0, double x1, double y1, double px, double py)
        {
            return (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
        }
    }
}
