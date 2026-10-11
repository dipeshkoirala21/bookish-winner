using System;
using System.Collections.Generic;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Horizon
{
    /// <summary>
    /// Builds the distant-mountains ring from a <see cref="HorizonPanorama"/> (engine-free; one instance per worker,
    /// allocation-free once its scratch fits): for every distance band, the silhouette is simplified (Douglas-Peucker in
    /// degrees, the tolerance growing until the tier's <see cref="HorizonSettings.TriangleCap"/> holds) and each kept
    /// point becomes a column of a curtain hung from the true silhouette point (its real position and altitude) down to
    /// <see cref="HorizonSettings.BottomDepressionDeg"/> below the eye's horizon. Every curated summit on its band's
    /// silhouette gets a column of its own at its exact position and published height. Positions are relative to the
    /// eye (x east, z north) with absolute altitudes in y; the skyline shader lowers them by the earth curvature like
    /// the rest of the world, shades them from altitude (forest, alpine, rock, snow) and haze, and draws them at the
    /// handover distance so nearer streamed terrain stays in front.
    /// <para>
    /// Vertex data: normal = a pseudo-normal that faces the eye and leans the way the silhouette falls (so each peak has
    /// a lit and a shaded flank); colour R = rock exposure (steep silhouette ground), G = band index (0 nearest), B = 0,
    /// A = 255; UV0 = (rib coordinate in metres, 0 at the top and 1 at the foot). The rib coordinate is the bearing of
    /// the column from the field's anchor (<see cref="HorizonPanorama.AnchorX"/>) times a fixed radius per band, so the
    /// shader's rib noise is continuous along a curtain and stays on the same ground when the ring is rebuilt for a new
    /// eye (a column pair is doubled where a curtain crosses the anchor's south, the coordinate's seam). The ring is
    /// drawn by its own shader, not ToonLit, so UV0 does not carry the material channel and AO of the detail-pass
    /// contract.
    /// </para>
    /// </summary>
    public sealed class HorizonMesher
    {
        private double[] _az = new double[0], _el = new double[0];
        private int[] _src = new int[0];
        private bool[] _keep = new bool[0], _force = new bool[0];
        private int[] _stack = new int[64];
        private readonly List<int> _runStarts = new List<int>(8), _runEnds = new List<int>(8);

        /// <summary>The tolerance (degrees) the last build ended with.</summary>
        public double ToleranceUsedDeg { get; private set; }

        /// <summary>Columns emitted by the last build (all bands).</summary>
        public int Columns { get; private set; }

        /// <summary>Summit columns emitted by the last build.</summary>
        public int SummitColumns { get; private set; }

        /// <summary>
        /// Clears <paramref name="m"/> and fills it with the ring for <paramref name="p"/>. Returns the triangle count
        /// (at most the settings' cap: the tolerance grows by a third until it fits).
        /// </summary>
        public int Build(HorizonPanorama p, HorizonSettings settings, MeshData m)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (m == null) throw new ArgumentNullException(nameof(m));
            double tol = Math.Max(1e-4, settings.SimplifyToleranceDeg);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                m.Clear();
                m.HasUv0 = true;
                Emit(p, settings, tol, m);
                if (m.TriangleCount <= settings.TriangleCap) break;
                tol *= 1.35;
            }
            ToleranceUsedDeg = tol;
            return m.TriangleCount;
        }

        private void Emit(HorizonPanorama p, HorizonSettings s, double tol, MeshData m)
        {
            int n = p.Azimuths;
            Ensure(n + 1 + p.SummitCount);
            double step = p.AzimuthStepDeg, k2 = HorizonPanorama.InverseTwoEffectiveRadius;
            double tanBottom = Math.Tan(s.BottomDepressionDeg * Math.PI / 180.0);
            int columns = 0, summits = 0;
            for (int band = 0; band < p.Bands; band++)
            {
                FindRuns(p, band, n);
                for (int r = 0; r < _runStarts.Count; r++)
                {
                    int first = _runStarts[r], count = _runEnds[r] - first; // rays first .. first + count - 1 (wrapping)
                    bool closed = count == n;
                    int rays = closed ? n + 1 : count;
                    if (rays < 2) continue;
                    int len = Gather(p, band, first, rays, step);
                    Simplify(len, tol);
                    int prevTop = -1, prevBottom = -1;
                    double ribRadius = Math.Sqrt(p.BandEdgesM[band] * p.BandEdgesM[band + 1]), lastBeta = 0.0;
                    for (int k = 0; k < len; k++)
                    {
                        if (!_keep[k]) continue;
                        double azDeg = _az[k], dist, alt, steep, lean;
                        float px, pz, gx = 0f, gz = 0f;
                        if (_src[k] >= 0)
                        {
                            int i = _src[k];
                            float tanE, fd, fa, fs;
                            p.TryGet(band, i, out tanE, out fd, out fa, out fs);
                            dist = fd;
                            alt = fa;
                            steep = fs;
                            double a = i * step * Math.PI / 180.0;
                            px = (float)(Math.Sin(a) * dist);
                            pz = (float)(Math.Cos(a) * dist);
                            lean = SilhouetteSlope(p, band, i, n, step);
                            p.GetGradient(band, i, out gx, out gz);
                        }
                        else
                        {
                            int sk = -_src[k] - 1, sb;
                            double saz, sx, sz;
                            float sd, sa;
                            p.GetSummit(sk, out sb, out saz, out sd, out sa, out sx, out sz);
                            dist = sd;
                            alt = sa;
                            px = (float)(sx - p.EyeX);
                            pz = (float)(sz - p.EyeZ);
                            int i0 = (int)Math.Floor(saz / step) % n;
                            float t0, d0, a0, s0;
                            p.TryGet(band, i0, out t0, out d0, out a0, out s0);
                            steep = s0;
                            lean = 0.0;
                            summits++;
                        }
                        double ar = azDeg * Math.PI / 180.0, dx = Math.Sin(ar), dz = Math.Cos(ar);
                        double tx = Math.Cos(ar), tz = -Math.Sin(ar); // direction of growing azimuth
                        double l = Math.Max(-2.0, Math.Min(2.0, lean)) * 0.8;
                        // The face below the silhouette: the ground's own normal there (its aspect turns it to or from
                        // the sun), leaning the way the silhouette falls, and turned towards the eye that sees it: steep, as the
                        // faces of a range seen from afar are, so the sun lights one flank and the sky the other.
                        double nx = -gx - tx * l - dx * 0.8, ny = 0.55, nz = -gz - tz * l - dz * 0.8;
                        double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        nx /= nl;
                        ny /= nl;
                        nz /= nl;
                        double bottom = p.EyeAltitudeM + dist * dist * k2 - dist * tanBottom;
                        bottom = Math.Min(bottom, alt - 200.0);
                        byte rock = (byte)Math.Max(0, Math.Min(255, (int)Math.Round((steep - 0.35) / 0.6 * 255.0)));
                        uint rgba = (uint)rock << 24 | (uint)band << 16 | 0xFFu;
                        // Rib coordinate: bearing from the anchor (seam due south of it) times the band's radius.
                        double beta = Math.Atan2(px + p.EyeX - p.AnchorX, pz + p.EyeZ - p.AnchorZ);
                        if (prevTop >= 0 && Math.Abs(beta - lastBeta) > Math.PI)
                        {
                            // Close the quad across the seam with the coordinate unwrapped, then start afresh.
                            double unwrapped = beta + (beta < lastBeta ? 2.0 : -2.0) * Math.PI;
                            int seamTop = m.AddVertex(px, (float)alt, pz, (float)nx, (float)ny, (float)nz, rgba, (float)(unwrapped * ribRadius), 0f);
                            int seamBot = m.AddVertex(px, (float)bottom, pz, (float)nx, (float)ny, (float)nz, rgba, (float)(unwrapped * ribRadius), 1f);
                            m.AddTriangle(prevTop, seamTop, seamBot);
                            m.AddTriangle(prevTop, seamBot, prevBottom);
                            prevTop = -1;
                        }
                        lastBeta = beta;
                        float along = (float)(beta * ribRadius);
                        int top = m.AddVertex(px, (float)alt, pz, (float)nx, (float)ny, (float)nz, rgba, along, 0f);
                        int bot = m.AddVertex(px, (float)bottom, pz, (float)nx, (float)ny, (float)nz, rgba, along, 1f);
                        if (prevTop >= 0)
                        {
                            // Front faces look at the eye (Unity winding, cross(b - a, c - a) towards the eye).
                            m.AddTriangle(prevTop, top, bot);
                            m.AddTriangle(prevTop, bot, prevBottom);
                        }
                        prevTop = top;
                        prevBottom = bot;
                        columns++;
                    }
                }
            }
            Columns = columns;
            SummitColumns = summits;
        }

        /// <summary>The points of one run: its rays in azimuth order with the band's summits merged in (forced).
        /// Returns the point count.</summary>
        private int Gather(HorizonPanorama p, int band, int first, int rays, double step)
        {
            int n = p.Azimuths;
            double a0 = first * step, a1 = (first + rays - 1) * step;
            int len = 0;
            for (int r = 0; r < rays; r++)
            {
                double az = (first + r) * step;
                // Summits between the previous ray and this one.
                if (r > 0)
                {
                    double prevAz = (first + r - 1) * step;
                    for (int sk = 0; sk < p.SummitCount; sk++)
                    {
                        int sb;
                        double saz, sx, sz;
                        float sd, sa;
                        p.GetSummit(sk, out sb, out saz, out sd, out sa, out sx, out sz);
                        if (sb != band) continue;
                        double u = saz;
                        while (u < a0) u += 360.0;
                        if (u > a1) continue;
                        if (u > prevAz && u <= az)
                        {
                            _az[len] = u;
                            _src[len] = -sk - 1;
                            _el[len] = Math.Atan((sa - p.EyeAltitudeM - (double)sd * sd * HorizonPanorama.InverseTwoEffectiveRadius) / sd) * 180.0 / Math.PI;
                            _keep[len] = true;
                            _force[len] = true;
                            len++;
                        }
                    }
                }
                int i = (first + r) % n;
                _az[len] = az;
                _src[len] = i;
                _el[len] = p.ElevationDeg(band, i);
                _keep[len] = false;
                _force[len] = false;
                len++;
            }
            return len;
        }

        /// <summary>Contiguous runs of rays with a silhouette in the band (wrapping round north), as [start, end) in ray
        /// units (end may exceed n).</summary>
        private void FindRuns(HorizonPanorama p, int band, int n)
        {
            _runStarts.Clear();
            _runEnds.Clear();
            int firstGap = -1;
            for (int i = 0; i < n; i++)
                if (!p.Has(band, i))
                {
                    firstGap = i;
                    break;
                }
            if (firstGap < 0)
            {
                // A whole circle: start due south, where Kathmandu sees no snow peaks.
                _runStarts.Add(n / 2);
                _runEnds.Add(n / 2 + n);
                return;
            }
            // Walk once round the circle starting just after a gap, so no run is split at north.
            int k = 0;
            while (k < n)
            {
                int i = (firstGap + 1 + k) % n;
                if (!p.Has(band, i))
                {
                    k++;
                    continue;
                }
                int start = firstGap + 1 + k, end = start;
                while (end - start < n && p.Has(band, end % n)) end++;
                _runStarts.Add(start % n);
                _runEnds.Add(start % n + (end - start));
                k += end - start;
            }
        }

        private static double SilhouetteSlope(HorizonPanorama p, int band, int i, int n, double stepDeg)
        {
            int a = (i - 2 + n) % n, b = (i + 2) % n;
            double ea = p.ElevationDeg(band, a), eb = p.ElevationDeg(band, b);
            if (double.IsNaN(ea)) ea = p.ElevationDeg(band, i);
            if (double.IsNaN(eb)) eb = p.ElevationDeg(band, i);
            return (eb - ea) / (4.0 * stepDeg);
        }

        /// <summary>Douglas-Peucker on (azimuth, elevation) in degrees with the vertical deviation from the chord as
        /// the error; marks the points kept in <see cref="_keep"/>: both ends, every forced point, and whatever the
        /// tolerance needs between them.</summary>
        private void Simplify(int len, double tol)
        {
            _keep[0] = true;
            _keep[len - 1] = true;
            int sp = 0, lo = 0;
            // Forced points split the run first.
            for (int k = 1; k < len; k++)
            {
                if (!_force[k] && k != len - 1) continue;
                Push(ref sp, lo, k);
                lo = k;
            }
            while (sp > 0)
            {
                int hi = _stack[--sp];
                lo = _stack[--sp];
                if (hi - lo < 2) continue;
                double a0 = _az[lo], e0 = _el[lo], a1 = _az[hi], e1 = _el[hi];
                double span = a1 - a0;
                int worst = -1;
                double worstErr = tol;
                for (int k = lo + 1; k < hi; k++)
                {
                    double t = span > 0 ? (_az[k] - a0) / span : 0.0;
                    double err = Math.Abs(_el[k] - (e0 + (e1 - e0) * t));
                    if (err > worstErr)
                    {
                        worstErr = err;
                        worst = k;
                    }
                }
                if (worst < 0) continue;
                _keep[worst] = true;
                Push(ref sp, lo, worst);
                Push(ref sp, worst, hi);
            }
        }

        private void Push(ref int sp, int lo, int hi)
        {
            if (sp + 2 > _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
            _stack[sp++] = lo;
            _stack[sp++] = hi;
        }

        private void Ensure(int len)
        {
            if (_az.Length >= len) return;
            _az = new double[len];
            _el = new double[len];
            _src = new int[len];
            _keep = new bool[len];
            _force = new bool[len];
        }
    }
}
