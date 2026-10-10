using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// A lathe profile for <see cref="RoadKit.Lathe"/>: points from the outside in (radius, height above the local
    /// surface), each with a colour, material channel, AO and a profile normal (radial outward, up); a break after a
    /// point duplicates the next one (a hard edge).
    /// </summary>
    internal sealed class LatheProfile
    {
        public double[] R = new double[24], H = new double[24], NR = new double[24], NH = new double[24];
        public uint[] C = new uint[24];
        public MaterialChannel[] Ch = new MaterialChannel[24];
        public float[] Ao = new float[24];
        public bool[] Break = new bool[24];
        public int Count;

        public void Clear()
        {
            Count = 0;
        }

        public void Add(double r, double h, double nr, double nh, uint c, MaterialChannel ch, float ao, bool breakAfter = false)
        {
            if (Count == R.Length)
            {
                int n = Count * 2;
                Array.Resize(ref R, n);
                Array.Resize(ref H, n);
                Array.Resize(ref NR, n);
                Array.Resize(ref NH, n);
                Array.Resize(ref C, n);
                Array.Resize(ref Ch, n);
                Array.Resize(ref Ao, n);
                Array.Resize(ref Break, n);
            }
            R[Count] = r;
            H[Count] = h;
            NR[Count] = nr;
            NH[Count] = nh;
            C[Count] = c;
            Ch[Count] = ch;
            Ao[Count] = ao;
            Break[Count] = breakAfter;
            Count++;
        }
    }

    /// <summary>
    /// Rounded road furniture for the junction mesher: lathes (island kerbs, aprons, planting strips, podiums, umbrellas)
    /// that follow the terrain plus a base lift, painted kerb bands (alternating colours per band, hard edges), and
    /// extruded kerbed outlines (splitter islands). Everything writes UV0 = (material channel, AO).
    /// </summary>
    internal static class RoadKit
    {
        /// <summary>Segments of a circle of radius r so a chord is at most <paramref name="chordM"/> long.</summary>
        public static int Segments(double r, double chordM, int min, int max)
        {
            int n = (int)Math.Ceiling(2 * Math.PI * Math.Max(0.01, r) / chordM);
            return n < min ? min : n > max ? max : n;
        }

        /// <summary>
        /// Sweep a profile round (cx, cz) in <paramref name="segs"/> segments. Heights: the terrain under each vertex plus
        /// <paramref name="baseLift"/> plus the profile height (<paramref name="flatBase"/>: the terrain at the centre for
        /// all, for small rigid objects such as podiums). A point at radius 0 closes the lathe with a fan.
        /// </summary>
        public static void Lathe(MeshData m, ref RoadSurface g, double cx, double cz, int segs, float baseLift, LatheProfile p, bool flatBase)
        {
            if (p.Count < 2 || segs < 3) return;
            float centreY = flatBase ? g.Height(cx, cz) : 0f;
            int[] rows = new int[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                rows[i] = m.VertexCount;
                double r = p.R[i];
                int n = r < 1e-4 ? 1 : segs + 1;
                for (int k = 0; k < n; k++)
                {
                    double a = 2 * Math.PI * k / segs, ca = Math.Cos(a), sa = Math.Sin(a);
                    double x = cx + r * ca, z = cz + r * sa;
                    float ground = flatBase ? centreY : g.Height(x, z);
                    float nx, ny, nz;
                    RoadSweep.Normalise((float)(p.NR[i] * ca), (float)p.NH[i], (float)(p.NR[i] * sa), out nx, out ny, out nz);
                    m.AddVertex((float)x, ground + baseLift + (float)p.H[i], (float)z, nx, ny, nz, p.C[i], RoadMaterials.U(p.Ch[i]), p.Ao[i]);
                }
            }
            for (int i = 0; i + 1 < p.Count; i++)
            {
                if (p.Break[i]) continue;
                int a = rows[i], b = rows[i + 1];
                bool ac = p.R[i] < 1e-4, bc = p.R[i + 1] < 1e-4;
                for (int k = 0; k < segs; k++)
                {
                    if (ac && bc) break;
                    if (ac) RoadSweep.Tri(m, a, b + k, b + k + 1, m.Normals[3 * (b + k)], m.Normals[3 * (b + k) + 1], m.Normals[3 * (b + k) + 2]);
                    else if (bc) RoadSweep.Tri(m, a + k, b, a + k + 1, m.Normals[3 * (a + k)], m.Normals[3 * (a + k) + 1], m.Normals[3 * (a + k) + 2]);
                    else RoadSweep.Quad(m, a + k, b + k, b + k + 1, a + k + 1);
                }
            }
        }

        /// <summary>
        /// A painted kerb round (cx, cz): the profile points (radius, height) swept in <paramref name="bands"/> segments, each
        /// with its own vertices so the bands alternate <paramref name="c0"/> and <paramref name="c1"/> with hard edges.
        /// </summary>
        public static void KerbBands(MeshData m, ref RoadSurface g, double cx, double cz, int bands, float baseLift, LatheProfile p, uint c0, uint c1,
                                     MaterialChannel ch)
        {
            if (p.Count < 2 || bands < 3) return;
            for (int k = 0; k < bands; k++)
            {
                uint c = (k & 1) == 0 ? c0 : c1;
                double a0 = 2 * Math.PI * k / bands, a1 = 2 * Math.PI * (k + 1) / bands;
                int first = m.VertexCount;
                for (int i = 0; i < p.Count; i++)
                {
                    for (int e = 0; e < 2; e++)
                    {
                        double a = e == 0 ? a0 : a1, ca = Math.Cos(a), sa = Math.Sin(a);
                        double x = cx + p.R[i] * ca, z = cz + p.R[i] * sa;
                        float nx, ny, nz;
                        RoadSweep.Normalise((float)(p.NR[i] * ca), (float)p.NH[i], (float)(p.NR[i] * sa), out nx, out ny, out nz);
                        m.AddVertex((float)x, g.Height(x, z) + baseLift + (float)p.H[i], (float)z, nx, ny, nz, c, RoadMaterials.U(ch), p.Ao[i]);
                    }
                }
                for (int i = 0; i + 1 < p.Count; i++)
                {
                    int a = first + 2 * i, b = first + 2 * (i + 1);
                    RoadSweep.Quad(m, a, b, b + 1, a + 1);
                }
            }
        }

        /// <summary>
        /// A raised kerbed outline (counter-clockwise polygon, tile-local metres) standing on the surface heights
        /// <paramref name="baseY"/> of its points (<paramref name="centreY"/> at the centroid): a mountable kerb face painted
        /// in alternating bands of about <paramref name="bandM"/>, a rounded crest and a flat top (fan from the centroid)
        /// in <paramref name="top"/>.
        /// </summary>
        public static void KerbedOutline(MeshData m, double[] px, double[] pz, float[] baseY, int n, float centreY, float height, double bandM,
                                         uint c0, uint c1, uint top, MaterialChannel topCh)
        {
            if (n < 3) return;
            double cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += px[i];
                cz += pz[i];
            }
            cx /= n;
            cz /= n;
            // Kerb face: per edge, own vertices (bands), from the outline at −0.04 up to the crest 0.1 m inward.
            int band = 0;
            float u = RoadMaterials.U(MaterialChannel.Paint);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double ex = px[j] - px[i], ez = pz[j] - pz[i], el = Math.Sqrt(ex * ex + ez * ez);
                if (el < 1e-6) continue;
                double ox = ez / el, oz = -ex / el; // outward of a counter-clockwise outline
                int pieces = Math.Max(1, (int)Math.Round(el / bandM));
                float nx, ny, nz, tx, ty, tz;
                RoadSweep.Normalise((float)ox * 0.7f, 0.7f, (float)oz * 0.7f, out nx, out ny, out nz);
                RoadSweep.Normalise((float)ox * 0.3f, 0.95f, (float)oz * 0.3f, out tx, out ty, out tz);
                for (int q = 0; q < pieces; q++)
                {
                    float f0 = (float)q / pieces, f1 = (float)(q + 1) / pieces;
                    uint c = (band++ & 1) == 0 ? c0 : c1;
                    double ax = px[i] + ex * f0, az = pz[i] + ez * f0, bx = px[i] + ex * f1, bz = pz[i] + ez * f1;
                    float ya = baseY[i] + (baseY[j] - baseY[i]) * f0, yb = baseY[i] + (baseY[j] - baseY[i]) * f1;
                    int v = m.AddVertex((float)ax, ya - 0.04f, (float)az, nx, ny, nz, c, u, 0.7f);
                    m.AddVertex((float)bx, yb - 0.04f, (float)bz, nx, ny, nz, c, u, 0.7f);
                    m.AddVertex((float)(bx - ox * 0.1), yb + height, (float)(bz - oz * 0.1), tx, ty, tz, c, u, 1f);
                    m.AddVertex((float)(ax - ox * 0.1), ya + height, (float)(az - oz * 0.1), tx, ty, tz, c, u, 1f);
                    RoadSweep.Quad(m, v, v + 3, v + 2, v + 1);
                }
            }
            // Top: the outline pulled 0.1 m inward, fanned from the centroid.
            int centre = m.VertexCount;
            float ut = RoadMaterials.U(topCh);
            m.AddVertex((float)cx, centreY + height + 0.01f, (float)cz, 0f, 1f, 0f, top, ut, 1f);
            int ring = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double dx = cx - px[i], dz = cz - pz[i], dl = Math.Sqrt(dx * dx + dz * dz);
                double k = dl > 1e-6 ? Math.Min(0.1, 0.5 * dl) / dl : 0;
                m.AddVertex((float)(px[i] + dx * k), baseY[i] + height, (float)(pz[i] + dz * k), 0f, 1f, 0f, top, ut, 0.9f);
            }
            for (int i = 0; i < n; i++) RoadSweep.Tri(m, centre, ring + i, ring + (i + 1) % n, 0, 1, 0);
        }

        /// <summary>A vertical cylinder (pole) of radius r from y0 to y1 at (cx, cz), UV0 = (channel, ao).</summary>
        public static void Pole(MeshData m, double cx, double cz, double r, float y0, float y1, int segs, uint c, MaterialChannel ch)
        {
            int first = m.VertexCount;
            for (int k = 0; k <= segs; k++)
            {
                double a = 2 * Math.PI * k / segs, ca = Math.Cos(a), sa = Math.Sin(a);
                m.AddVertex((float)(cx + r * ca), y0, (float)(cz + r * sa), (float)ca, 0f, (float)sa, c, RoadMaterials.U(ch), 0.7f);
                m.AddVertex((float)(cx + r * ca), y1, (float)(cz + r * sa), (float)ca, 0f, (float)sa, c, RoadMaterials.U(ch), 1f);
            }
            for (int k = 0; k < segs; k++)
            {
                int a = first + 2 * k;
                RoadSweep.Quad(m, a, a + 2, a + 3, a + 1);
            }
        }
    }
}
