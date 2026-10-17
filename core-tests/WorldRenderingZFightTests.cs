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
        /// <summary>Surfaces closer than this along their shared normal can fight: exactly coplanar faces on every depth
        /// buffer, and up to about a millimetre on GLES3 (no reversed Z) at the near ring's reach, where one depth step is
        /// d² / (near · 2^24) ≈ 0.15 mm at 32 m with the 0.4 m near plane, and the vertices themselves are rounded to
        /// 0.06 mm about 1 km from the tile origin. (Metal, Vulkan and D3D render reversed-Z into the 32-bit float depth
        /// attachment ProjectSetup asks for: far finer.)</summary>
        public const float FightM = 0.001f;

        /// <summary>Overlaps smaller than this (m²) are below a few pixels even up close.</summary>
        public const float MinOverlapM2 = 0.0004f;

        /// <summary>Before the fixes (same scan, near and lite rings, the three spots, measured on the playtest base
        /// 64155f3 with the house kit as it was): 9 778 visibly different coplanar pairs over 405 m², 291 m² of them exactly
        /// coplanar (balcony slabs on the floor bands, the gable soffits under the slopes' own, weathering patches and
        /// streaks on one layer, signs on window ledges and frames, every relief element a road clips ending on the same
        /// plane, shop posts sunk into the reveals, blade-sign lettering 1 mm off the blade, pilasters and bands clipped to
        /// one depth). On the first base this scan ran on (near ring only): 13 132 pairs, 517 m².</summary>
        public const double BeforeAreaM2 = 405.0;

        /// <summary>... of which exactly coplanar (m²).</summary>
        public const double BeforeExactAreaM2 = 291.0;

        [Test]
        public void HousesHaveNoCoplanarOverlappingFaces()
        {
            var report = new StringBuilder();
            double total = 0, exact = 0;
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                CoplanarScan near = CoplanarScan.Run(scene.Houses, FightM, MinOverlapM2);
                CoplanarScan lite = CoplanarScan.Run(scene.LiteHouses, FightM, MinOverlapM2);
                report.Append(spot.Name).Append(" B0 near ring: ").Append(near).Append('\n');
                report.Append(spot.Name).Append(" B0 lite ring: ").Append(lite).Append('\n');
                total += near.AreaM2 + lite.AreaM2;
                exact += near.ExactAreaM2 + lite.ExactAreaM2;
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("zfight_houses.txt", report.ToString());
            // The long tail left (11 m², 2.7%; 4.9 m² exactly coplanar, 1.7%) is grammar overlap: an awning over a carved
            // window, shutters on brick pilasters, two lattice parts at one depth (by channel pair in the report): open issue
            // for the buildings package (World/README.md). It must stay a small fraction of what flickered before.
            Assert.That(total, Is.LessThan(0.04 * BeforeAreaM2), "coplanar overlapping house faces (z-fighting):\n" + report);
            Assert.That(exact, Is.LessThan(0.025 * BeforeExactAreaM2), "exactly coplanar overlapping house faces:\n" + report);
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
                foreach (int layer in new[] { TileLayers.Roads, TileLayers.RoadDecals, TileLayers.Areas, TileLayers.Bridges, TileLayers.Ornaments })
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
    /// Everything is measured in double precision relative to the mesh's centre (plane offsets taken at ~1 km from the
    /// tile origin would carry centimetres of error for a small triangle). Triangles are bucketed by quantised normal
    /// and plane offset; candidates in neighbouring buckets are clipped against each other in the first one's plane
    /// (Sutherland-Hodgman), and the gap is the distance between the two planes at the centre of the overlap.
    /// </summary>
    internal sealed class CoplanarScan
    {
        public int Triangles;

        /// <summary>Overlapping pairs whose surfaces look different (colour, material channel or baked AO): the fight
        /// shows as flicker.</summary>
        public int Pairs;

        public double AreaM2;

        /// <summary>... of which exactly coplanar (gap under 0.1 mm: they fight on every depth buffer).</summary>
        public int Exact;

        public double ExactAreaM2;

        /// <summary>Overlapping pairs that shade the same (identical colour, channel and AO): the fight is invisible.</summary>
        public int SamePairs;

        public double SameAreaM2;
        public readonly List<string> Examples = new List<string>();

        /// <summary>Area of the visibly different pairs by material channel pair (lower channel first): which emitters of
        /// the grammar still overlap.</summary>
        public readonly Dictionary<string, double> ByChannels = new Dictionary<string, double>();

        /// <summary>Gap below which a pair counts as exactly coplanar (m).</summary>
        public const double ExactM = 0.0001;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Triangles).Append(" triangles, ").Append(Pairs).Append(" visibly different coplanar overlapping pairs, ")
              .Append(AreaM2.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m² (").Append(Exact).Append(" exactly coplanar, ")
              .Append(ExactAreaM2.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m²); ").Append(SamePairs)
              .Append(" identical-looking pairs, ").Append(SameAreaM2.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m²");
            if (ByChannels.Count > 0)
            {
                var pairs = new List<KeyValuePair<string, double>>(ByChannels);
                pairs.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.Append("; by channels:");
                for (int i = 0; i < pairs.Count && i < 6; i++)
                    sb.Append(' ').Append(pairs[i].Key).Append(' ').Append(pairs[i].Value.ToString("0.00", CultureInfo.InvariantCulture)).Append(" m²");
            }
            foreach (string e in Examples) sb.Append("\n    ").Append(e);
            return sb.ToString();
        }

        /// <summary>Called for every visibly different pair: triangle indices, overlap area and gap (diagnostics).</summary>
        public Action<int, int, double, double> OnPair;

        private double _ox, _oy, _oz;
        private double[] _p;

        public static CoplanarScan Run(MeshData m, float fightM, float minOverlapM2, int maxExamples = 12, Action<int, int, double, double> onPair = null)
        {
            var scan = new CoplanarScan { OnPair = onPair };
            int tris = m.IndexCount / 3;
            scan.Triangles = tris;
            if (tris == 0) return scan;
            // Local frame: the centre of the mesh's bounds.
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            for (int v = 0; v < m.VertexCount; v++)
            {
                x0 = Math.Min(x0, m.Positions[3 * v]);
                x1 = Math.Max(x1, m.Positions[3 * v]);
                y0 = Math.Min(y0, m.Positions[3 * v + 1]);
                y1 = Math.Max(y1, m.Positions[3 * v + 1]);
                z0 = Math.Min(z0, m.Positions[3 * v + 2]);
                z1 = Math.Max(z1, m.Positions[3 * v + 2]);
            }
            scan._ox = 0.5 * (x0 + x1);
            scan._oy = 0.5 * (y0 + y1);
            scan._oz = 0.5 * (z0 + z1);
            scan._p = new double[3 * m.VertexCount];
            for (int v = 0; v < m.VertexCount; v++)
            {
                scan._p[3 * v] = m.Positions[3 * v] - scan._ox;
                scan._p[3 * v + 1] = m.Positions[3 * v + 1] - scan._oy;
                scan._p[3 * v + 2] = m.Positions[3 * v + 2] - scan._oz;
            }
            double[] P = scan._p;
            var nx = new double[tris];
            var ny = new double[tris];
            var nz = new double[tris];
            var d = new double[tris];
            var keys = new long[tris];
            var buckets = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                double ax = P[3 * a], ay = P[3 * a + 1], az = P[3 * a + 2];
                double e1x = P[3 * b] - ax, e1y = P[3 * b + 1] - ay, e1z = P[3 * b + 2] - az;
                double e2x = P[3 * c] - ax, e2y = P[3 * c + 1] - ay, e2z = P[3 * c + 2] - az;
                double qx = e1y * e2z - e1z * e2y, qy = e1z * e2x - e1x * e2z, qz = e1x * e2y - e1y * e2x;
                double l = Math.Sqrt(qx * qx + qy * qy + qz * qz);
                keys[t] = long.MinValue;
                if (0.5 * l < minOverlapM2) continue;
                nx[t] = qx / l;
                ny[t] = qy / l;
                nz[t] = qz / l;
                d[t] = nx[t] * ax + ny[t] * ay + nz[t] * az;
                long key = Key(nx[t], ny[t], nz[t], d[t], 0, 0, 0, 0);
                keys[t] = key;
                List<int> list;
                if (!buckets.TryGetValue(key, out list)) buckets[key] = list = new List<int>();
                list.Add(t);
            }
            var pa = new double[6];
            var pb = new double[6];
            for (int t = 0; t < tris; t++)
            {
                if (keys[t] == long.MinValue) continue;
                for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                for (int k = -1; k <= 1; k++)
                for (int o = -1; o <= 1; o++)
                {
                    List<int> list;
                    if (!buckets.TryGetValue(Key(nx[t], ny[t], nz[t], d[t], i, j, k, o), out list)) continue;
                    for (int q = 0; q < list.Count; q++)
                        if (list[q] > t) scan.Test(m, t, list[q], nx, ny, nz, fightM, minOverlapM2, maxExamples, pa, pb);
                }
            }
            return scan;
        }

        private const double NormalStep = 0.05, OffsetStep = 0.05;

        private static long Key(double nx, double ny, double nz, double d, int i, int j, int k, int o)
        {
            long qx = (long)Math.Round(nx / NormalStep) + 64 + i, qy = (long)Math.Round(ny / NormalStep) + 64 + j, qz = (long)Math.Round(nz / NormalStep) + 64 + k;
            long qd = (long)Math.Floor(d / OffsetStep) + o + (1L << 30);
            return (((qx * 128 + qy) * 128 + qz) << 32) ^ qd;
        }

        private void Test(MeshData m, int t, int u, double[] nx, double[] ny, double[] nz, float fightM, float minOverlapM2, int maxExamples,
                          double[] pa, double[] pb)
        {
            double dot = nx[t] * nx[u] + ny[t] * ny[u] + nz[t] * nz[u];
            if (dot < 0.9995) return;
            double[] P = _p;
            int ta = m.Indices[3 * t];
            double ax = P[3 * ta], ay = P[3 * ta + 1], az = P[3 * ta + 2];
            // Quick reject: u's vertices all farther than the fight distance (plus slack for the tilt) from t's plane.
            double near = double.MaxValue, far = double.MinValue;
            for (int k = 0; k < 3; k++)
            {
                int b = m.Indices[3 * u + k];
                double s = nx[t] * (P[3 * b] - ax) + ny[t] * (P[3 * b + 1] - ay) + nz[t] * (P[3 * b + 2] - az);
                near = Math.Min(near, s);
                far = Math.Max(far, s);
            }
            if (near > fightM || far < -fightM) return;
            // A basis of t's plane.
            double ux, uy, uz;
            if (Math.Abs(nx[t]) < 0.9)
            {
                ux = 0.0;
                uy = nz[t];
                uz = -ny[t];
            }
            else
            {
                ux = -nz[t];
                uy = 0.0;
                uz = nx[t];
            }
            double ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            double vx = ny[t] * uz - nz[t] * uy, vy = nz[t] * ux - nx[t] * uz, vz = nx[t] * uy - ny[t] * ux;
            for (int k = 0; k < 3; k++)
            {
                int a = m.Indices[3 * t + k], b = m.Indices[3 * u + k];
                double rx = P[3 * a] - ax, ry = P[3 * a + 1] - ay, rz = P[3 * a + 2] - az;
                pa[2 * k] = rx * ux + ry * uy + rz * uz;
                pa[2 * k + 1] = rx * vx + ry * vy + rz * vz;
                rx = P[3 * b] - ax;
                ry = P[3 * b + 1] - ay;
                rz = P[3 * b + 2] - az;
                pb[2 * k] = rx * ux + ry * uy + rz * uz;
                pb[2 * k + 1] = rx * vx + ry * vy + rz * vz;
            }
            if (Math.Max(pa[0], Math.Max(pa[2], pa[4])) <= Math.Min(pb[0], Math.Min(pb[2], pb[4])) ||
                Math.Max(pb[0], Math.Max(pb[2], pb[4])) <= Math.Min(pa[0], Math.Min(pa[2], pa[4])) ||
                Math.Max(pa[1], Math.Max(pa[3], pa[5])) <= Math.Min(pb[1], Math.Min(pb[3], pb[5])) ||
                Math.Max(pb[1], Math.Max(pb[3], pb[5])) <= Math.Min(pa[1], Math.Min(pa[3], pa[5])))
                return;
            double cu, cv;
            double overlap = ClipArea(pa, pb, out cu, out cv);
            if (overlap < minOverlapM2) return;
            // The gap at the overlap's centre: from t's plane along t's normal to u's plane.
            double px = ax + cu * ux + cv * vx, py = ay + cu * uy + cv * vy, pz = az + cu * uz + cv * vz;
            int ub = m.Indices[3 * u];
            double su = nx[u] * (px - P[3 * ub]) + ny[u] * (py - P[3 * ub + 1]) + nz[u] * (pz - P[3 * ub + 2]);
            double gap = Math.Abs(su / dot);
            if (gap > fightM) return;
            if (LookAlike(m, t, u, px, py, pz, px - su * nx[u], py - su * ny[u], pz - su * nz[u]))
            {
                SamePairs++;
                SameAreaM2 += overlap;
                return;
            }
            Pairs++;
            AreaM2 += overlap;
            if (gap < ExactM)
            {
                Exact++;
                ExactAreaM2 += overlap;
            }
            if (m.HasUv0)
            {
                int ca = (int)Math.Round(m.Uv0[2 * m.Indices[3 * t]]), cb = (int)Math.Round(m.Uv0[2 * m.Indices[3 * u]]);
                string key = (MaterialChannel)Math.Min(ca, cb) + "/" + (MaterialChannel)Math.Max(ca, cb);
                double sum;
                ByChannels.TryGetValue(key, out sum);
                ByChannels[key] = sum + overlap;
            }
            OnPair?.Invoke(t, u, overlap, gap);
            if (Examples.Count < maxExamples)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * u];
                string ch = m.HasUv0
                    ? " channels " + (int)Math.Round(m.Uv0[2 * a]) + "/" + (int)Math.Round(m.Uv0[2 * b])
                    : "";
                Examples.Add(string.Format(CultureInfo.InvariantCulture, "at ({0:0.00}, {1:0.00}, {2:0.00}) n ({3:0.00}, {4:0.00}, {5:0.00}) gap {6:0.0000} m overlap {7:0.0000} m²{8} colours #{9:X2}{10:X2}{11:X2}/#{12:X2}{13:X2}{14:X2}",
                                           px + _ox, py + _oy, pz + _oz, nx[t], ny[t], nz[t], gap, overlap, ch,
                                           m.Colors[4 * a], m.Colors[4 * a + 1], m.Colors[4 * a + 2], m.Colors[4 * b], m.Colors[4 * b + 1], m.Colors[4 * b + 2]));
            }
        }

        /// <summary>Both triangles shade alike where they overlap: at the overlap's centre (<paramref name="tx"/>... on t,
        /// <paramref name="ux"/>... on u) the interpolated vertex colours lie within 6/255, the material channels match and
        /// the baked AO lies within 0.06. Two copies of one surface (a ledge drawn twice) fight invisibly.</summary>
        private bool LookAlike(MeshData m, int t, int u, double tx, double ty, double tz, double ux, double uy, double uz)
        {
            double r0, g0, b0, a0, c0, r1, g1, b1, a1, c1;
            Shade(m, t, tx, ty, tz, out r0, out g0, out b0, out a0, out c0);
            Shade(m, u, ux, uy, uz, out r1, out g1, out b1, out a1, out c1);
            if (Math.Abs(r0 - r1) > 6 || Math.Abs(g0 - g1) > 6 || Math.Abs(b0 - b1) > 6) return false;
            if (Math.Abs(c0 - c1) > 0.25) return false;
            return Math.Abs(a0 - a1) <= 0.06;
        }

        /// <summary>Vertex colour, AO and channel of triangle t interpolated at a point of its plane (local frame).</summary>
        private void Shade(MeshData m, int t, double x, double y, double z, out double r, out double g, out double b, out double ao, out double ch)
        {
            double[] P = _p;
            int i0 = m.Indices[3 * t], i1 = m.Indices[3 * t + 1], i2 = m.Indices[3 * t + 2];
            double e1x = P[3 * i1] - P[3 * i0], e1y = P[3 * i1 + 1] - P[3 * i0 + 1], e1z = P[3 * i1 + 2] - P[3 * i0 + 2];
            double e2x = P[3 * i2] - P[3 * i0], e2y = P[3 * i2 + 1] - P[3 * i0 + 1], e2z = P[3 * i2 + 2] - P[3 * i0 + 2];
            double qx = x - P[3 * i0], qy = y - P[3 * i0 + 1], qz = z - P[3 * i0 + 2];
            double d11 = e1x * e1x + e1y * e1y + e1z * e1z, d12 = e1x * e2x + e1y * e2y + e1z * e2z, d22 = e2x * e2x + e2y * e2y + e2z * e2z;
            double q1 = qx * e1x + qy * e1y + qz * e1z, q2 = qx * e2x + qy * e2y + qz * e2z;
            double den = d11 * d22 - d12 * d12;
            double w1 = den > 1e-18 ? (d22 * q1 - d12 * q2) / den : 1.0 / 3.0, w2 = den > 1e-18 ? (d11 * q2 - d12 * q1) / den : 1.0 / 3.0;
            w1 = Math.Max(0, Math.Min(1, w1));
            w2 = Math.Max(0, Math.Min(1 - w1, w2));
            double w0 = 1 - w1 - w2;
            r = w0 * m.Colors[4 * i0] + w1 * m.Colors[4 * i1] + w2 * m.Colors[4 * i2];
            g = w0 * m.Colors[4 * i0 + 1] + w1 * m.Colors[4 * i1 + 1] + w2 * m.Colors[4 * i2 + 1];
            b = w0 * m.Colors[4 * i0 + 2] + w1 * m.Colors[4 * i1 + 2] + w2 * m.Colors[4 * i2 + 2];
            if (m.HasUv0)
            {
                ao = w0 * m.Uv0[2 * i0 + 1] + w1 * m.Uv0[2 * i1 + 1] + w2 * m.Uv0[2 * i2 + 1];
                ch = w0 * m.Uv0[2 * i0] + w1 * m.Uv0[2 * i1] + w2 * m.Uv0[2 * i2];
            }
            else ao = ch = 0;
        }

        private readonly List<double> _poly = new List<double>(32), _next = new List<double>(32);

        /// <summary>Area and centroid of the intersection of two 2D triangles (any winding).</summary>
        private double ClipArea(double[] a, double[] b, out double cx, out double cy)
        {
            cx = cy = 0;
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
                double cr = _poly[2 * i] * _poly[2 * j + 1] - _poly[2 * j] * _poly[2 * i + 1];
                area += cr;
                cx += (_poly[2 * i] + _poly[2 * j]) * cr;
                cy += (_poly[2 * i + 1] + _poly[2 * j + 1]) * cr;
            }
            if (Math.Abs(area) < 1e-14) return 0;
            cx /= 3.0 * area;
            cy /= 3.0 * area;
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
