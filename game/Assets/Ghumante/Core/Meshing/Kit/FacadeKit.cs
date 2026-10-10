using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>A rectangular opening in a facade plane (frame coordinates u along the wall, v up).</summary>
    public struct KitHole
    {
        public double U0, U1, V0, V1;

        public KitHole(double u0, double v0, double u1, double v1)
        {
            U0 = u0;
            V0 = v0;
            U1 = u1;
            V1 = v1;
        }
    }

    /// <summary>
    /// Facade primitives for the building grammar (docs/W2_DETAIL_CONTRACT.md decision 6): walls with real openings
    /// and reveals, swept mouldings (cornices, bands, copings, sills) with smooth normals, lattice infill, corrugated
    /// panels (roller shutters, CGI), rails and posts. All work in a <see cref="KitFrame"/> (u along the wall, v up, w
    /// out of the wall). Positions, normals and colours only: channels and AO come from a <see cref="KitPaint"/> pass
    /// over the emitted range. Thread-static scratch; no allocation once warm.
    /// </summary>
    public static class FacadeKit
    {
        [ThreadStatic] private static double[] _vb;
        [ThreadStatic] private static int[] _act, _prev;

        private static void Scratch(int n)
        {
            if (_vb != null && _vb.Length >= n) return;
            int cap = Math.Max(64, n);
            _vb = new double[cap];
            _act = new int[cap];
            _prev = new int[cap];
        }

        /// <summary>
        /// The wall plane w over [u0, u1] × [v0, v1] facing +W with the given holes cut out (holes are clipped to the
        /// rectangle; overlapping holes merge). The wall is split into horizontal slabs at the hole edges and each slab
        /// into the pieces between holes; slabs with the same holes are merged, so a storey with a row of equal
        /// windows costs (windows + 3) quads. Returns the triangles added.
        /// </summary>
        public static int WallWithHoles(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w,
                                        KitHole[] holes, int n, uint c)
        {
            if (u1 - u0 < 1e-4 || v1 - v0 < 1e-4) return 0;
            Scratch(2 * n + 4);
            int nb = 0;
            _vb[nb++] = v0;
            _vb[nb++] = v1;
            for (int i = 0; i < n; i++)
            {
                KitHole h = holes[i];
                if (h.U1 <= u0 || h.U0 >= u1 || h.V1 <= v0 || h.V0 >= v1) continue;
                _vb[nb++] = Math.Max(v0, h.V0);
                _vb[nb++] = Math.Min(v1, h.V1);
            }
            Array.Sort(_vb, 0, nb);
            int k = 0;
            for (int i = 0; i < nb; i++)
                if (k == 0 || _vb[i] - _vb[k - 1] > 1e-5) _vb[k++] = _vb[i];
            nb = k;
            int tris = 0, prevCount = -1;
            double slab0 = v0;
            for (int s = 0; s + 1 < nb; s++)
            {
                double a = _vb[s], b = _vb[s + 1], mid = 0.5 * (a + b);
                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    KitHole h = holes[i];
                    if (h.V0 <= mid && h.V1 >= mid && h.U1 > u0 && h.U0 < u1) _act[count++] = i;
                }
                // Sort the active holes by U0 (few: insertion sort).
                for (int i = 1; i < count; i++)
                {
                    int x = _act[i], j = i - 1;
                    while (j >= 0 && holes[_act[j]].U0 > holes[x].U0)
                    {
                        _act[j + 1] = _act[j];
                        j--;
                    }
                    _act[j + 1] = x;
                }
                bool same = count == prevCount;
                for (int i = 0; same && i < count; i++) same = _act[i] == _prev[i];
                if (!same && s > 0)
                {
                    tris += Gaps(m, f, u0, u1, slab0, a, w, holes, _prev, prevCount, c);
                    slab0 = a;
                }
                for (int i = 0; i < count; i++) _prev[i] = _act[i];
                prevCount = count;
            }
            tris += Gaps(m, f, u0, u1, slab0, v1, w, holes, _prev, Math.Max(0, prevCount), c);
            return tris;
        }

        private static int Gaps(MeshData m, in KitFrame f, double u0, double u1, double a, double b, double w, KitHole[] holes, int[] act, int count,
                                uint c)
        {
            if (b - a < 1e-5) return 0;
            int tris = 0;
            double u = u0;
            for (int i = 0; i < count; i++)
            {
                KitHole h = holes[act[i]];
                double hu0 = Math.Max(u0, h.U0), hu1 = Math.Min(u1, h.U1);
                if (hu0 - u > 1e-5) tris += MeshKit.Panel(m, f, u, a, hu0, b, w, c);
                if (hu1 > u) u = hu1;
            }
            if (u1 - u > 1e-5) tris += MeshKit.Panel(m, f, u, a, u1, b, w, c);
            return tris;
        }

        /// <summary>The four reveal faces of a hole from the wall plane w back to w − depth (jambs face into the
        /// opening, the head faces down, the sill up). Returns the triangles added.</summary>
        public static int Reveal(MeshData m, in KitFrame f, in KitHole h, double w, double depth, uint c, bool sill = true)
        {
            if (depth <= 1e-4) return 0;
            double b = w - depth;
            int t = 0;
            t += MeshKit.QuadLocal(m, f, h.U0, h.V0, w, h.U0, h.V0, b, h.U0, h.V1, b, h.U0, h.V1, w, 1, 0, 0, c);
            t += MeshKit.QuadLocal(m, f, h.U1, h.V0, w, h.U1, h.V0, b, h.U1, h.V1, b, h.U1, h.V1, w, -1, 0, 0, c);
            t += MeshKit.QuadLocal(m, f, h.U0, h.V1, w, h.U1, h.V1, w, h.U1, h.V1, b, h.U0, h.V1, b, 0, -1, 0, c);
            if (sill) t += MeshKit.QuadLocal(m, f, h.U0, h.V0, w, h.U1, h.V0, w, h.U1, h.V0, b, h.U0, h.V0, b, 0, 1, 0, c);
            return t;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Sweeps
        // -------------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] _pw, _pv, _cx, _cz;
        [ThreadStatic] private static int[] _ct, _cn, _cp;

        /// <summary>The thread's profile scratch (w, v pairs) for <see cref="SweepU"/>.</summary>
        public static double[] ProfileW
        {
            get
            {
                Profile(32);
                return _pw;
            }
        }

        public static double[] ProfileV
        {
            get
            {
                Profile(32);
                return _pv;
            }
        }

        private static void Profile(int n)
        {
            if (_pw != null && _pw.Length >= n) return;
            _pw = new double[Math.Max(32, n)];
            _pv = new double[Math.Max(32, n)];
            _cx = new double[Math.Max(34, n + 2)];
            _cz = new double[Math.Max(34, n + 2)];
            _ct = new int[3 * Math.Max(34, n + 2)];
            _cn = new int[Math.Max(34, n + 2)];
            _cp = new int[Math.Max(34, n + 2)];
        }

        /// <summary>
        /// Sweep a profile along U over [u0, u1]: <paramref name="count"/> points (w, v) of <see cref="ProfileW"/> /
        /// <see cref="ProfileV"/>, listed from the wall at the bottom, out and up, back to the wall at the top (the
        /// outside is on the right of the walk, so the normal of a segment is (dv, −dw)). Normals are smooth across
        /// turns gentler than <paramref name="hardDeg"/>. Caps (bit 1 at u0, bit 2 at u1) close the profile against the
        /// wall plane w = 0 by ear clipping. Returns the triangles added.
        /// </summary>
        public static int SweepU(MeshData m, in KitFrame f, double u0, double u1, int count, int caps, uint c, double hardDeg = 50)
        {
            if (count < 2 || u1 - u0 < 1e-5) return 0;
            double[] pw = _pw, pv = _pv;
            double cosHard = Math.Cos(hardDeg * Math.PI / 180.0);
            int tris = 0;
            for (int s = 0; s + 1 < count; s++)
            {
                double dw = pw[s + 1] - pw[s], dv = pv[s + 1] - pv[s], l = Math.Sqrt(dw * dw + dv * dv);
                if (l < 1e-7) continue;
                double nw = dv / l, nv = -dw / l;
                double aw, av, bw, bv;
                Joint(pw, pv, count, s, s, nw, nv, cosHard, out aw, out av);
                Joint(pw, pv, count, s + 1, s, nw, nv, cosHard, out bw, out bv);
                double x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3;
                f.ToWorld(u0, pv[s], pw[s], out x0, out y0, out z0);
                f.ToWorld(u1, pv[s], pw[s], out x1, out y1, out z1);
                f.ToWorld(u1, pv[s + 1], pw[s + 1], out x2, out y2, out z2);
                f.ToWorld(u0, pv[s + 1], pw[s + 1], out x3, out y3, out z3);
                double anx = aw * f.WX, anz = aw * f.WZ, bnx = bw * f.WX, bnz = bw * f.WZ;
                tris += KitRound.QuadSmooth(m, x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3, anx, av, anz, anx, av, anz, bnx, bv, bnz, bnx, bv, bnz, c);
            }
            for (int e = 0; e < 2; e++)
            {
                if ((caps & (1 << e)) == 0) continue;
                double u = e == 0 ? u0 : u1, su = e == 0 ? -1 : 1;
                // The profile polygon in (w, v) closed along the wall; make it counter-clockwise in (w, v).
                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    if (n > 0 && Math.Abs(_cx[n - 1] - pw[i]) < 1e-7 && Math.Abs(_cz[n - 1] - pv[i]) < 1e-7) continue;
                    _cx[n] = pw[i];
                    _cz[n] = pv[i];
                    n++;
                }
                if (n < 3) continue;
                double area = Polygon.SignedArea(_cx, _cz, n);
                if (Math.Abs(area) < 1e-8) continue;
                if (area < 0)
                {
                    Array.Reverse(_cx, 0, n);
                    Array.Reverse(_cz, 0, n);
                }
                int t = Polygon.Triangulate(_cx, _cz, n, _ct, _cn, _cp, false);
                for (int i = 0; i < t; i++)
                {
                    int a = _ct[3 * i], b = _ct[3 * i + 1], d = _ct[3 * i + 2];
                    tris += MeshKit.TriLocal(m, f, u, _cz[a], _cx[a], u, _cz[b], _cx[b], u, _cz[d], _cx[d], su, 0, 0, c);
                }
            }
            return tris;
        }

        private static void Joint(double[] pw, double[] pv, int count, int k, int s, double snw, double snv, double cosHard, out double nw, out double nv)
        {
            nw = snw;
            nv = snv;
            int o = k == s ? s - 1 : s + 1;
            if (o < 0 || o + 1 >= count) return;
            double dw = pw[o + 1] - pw[o], dv = pv[o + 1] - pv[o], l = Math.Sqrt(dw * dw + dv * dv);
            if (l < 1e-9) return;
            double onw = dv / l, onv = -dw / l;
            if (onw * snw + onv * snv < cosHard) return;
            nw = snw + onw;
            nv = snv + onv;
            double nl = Math.Sqrt(nw * nw + nv * nv);
            if (nl < 1e-9)
            {
                nw = snw;
                nv = snv;
                return;
            }
            nw /= nl;
            nv /= nl;
        }

        /// <summary>A projecting band of depth <paramref name="d"/> and height <paramref name="h"/> whose bottom sits
        /// at v, with its outer edges rounded by <paramref name="r"/> (a bevelled slab edge, sill, coping or cornice
        /// course). Caps per <see cref="SweepU"/>. Returns the triangles added.</summary>
        public static int Band(MeshData m, in KitFrame f, double u0, double u1, double v, double h, double d, double r, int caps, uint c)
        {
            if (h <= 1e-5 || d <= 1e-5) return 0;
            Profile(16);
            r = Math.Max(0, Math.Min(r, 0.5 * Math.Min(h, d) - 1e-4));
            int k = 0;
            P(ref k, 0, v);
            if (r > 1e-4)
            {
                P(ref k, d - r, v);
                P(ref k, d - 0.2929 * r, v + 0.2929 * r);
                P(ref k, d, v + r);
                P(ref k, d, v + h - r);
                P(ref k, d - 0.2929 * r, v + h - 0.2929 * r);
                P(ref k, d - r, v + h);
            }
            else
            {
                P(ref k, d, v);
                P(ref k, d, v + h);
            }
            P(ref k, 0, v + h);
            return SweepU(m, f, u0, u1, k, caps, c);
        }

        /// <summary>A cheap ledge (sill, lintel with ears, sunshade): bottom at v, height h, projecting d, with its lower
        /// front edge chamfered by <paramref name="ch"/> (smooth-shaded, so it reads rounded): 3 quads plus optional end
        /// caps of 2 triangles each.</summary>
        public static int Ledge(MeshData m, in KitFrame f, double u0, double u1, double v, double h, double d, double ch, int caps, uint c)
        {
            if (h <= 1e-5 || d <= 1e-5) return 0;
            Profile(8);
            ch = Math.Max(0.002, Math.Min(ch, 0.6 * Math.Min(h, d)));
            int k = 0;
            P(ref k, 0, v);
            P(ref k, d - ch, v);
            P(ref k, d, v + ch);
            P(ref k, d, v + h);
            P(ref k, 0, v + h);
            return SweepU(m, f, u0, u1, k, caps, c, 60);
        }

        /// <summary>A stepped (corbelled) brick cornice: <paramref name="steps"/> courses, each <paramref name="course"/>
        /// high and projecting <paramref name="step"/> more than the one below, with a rounded top lip. Bottom at v.</summary>
        public static int Corbel(MeshData m, in KitFrame f, double u0, double u1, double v, int steps, double course, double step, int caps, uint c)
        {
            Profile(4 * steps + 4);
            int k = 0;
            P(ref k, 0, v);
            for (int i = 0; i < steps; i++)
            {
                double d = step * (i + 1), y0 = v + course * i, y1 = y0 + course;
                P(ref k, d, y0);
                if (i == steps - 1)
                {
                    P(ref k, d, y1 - 0.3 * course);
                    P(ref k, d - 0.3 * course, y1);
                }
                else P(ref k, d, y1);
            }
            P(ref k, 0, v + course * steps);
            return SweepU(m, f, u0, u1, k, caps, c, 35);
        }

        /// <summary>A parapet coping: a rounded cap of width <paramref name="width"/> centred on the wall line
        /// (w ∈ [−width/2, +width/2]), from v to v + h, overhanging both faces.</summary>
        public static int Coping(MeshData m, in KitFrame f, double u0, double u1, double v, double h, double width, int caps, uint c)
        {
            Profile(16);
            double a = -0.5 * width, b = 0.5 * width, r = Math.Min(0.45 * h, 0.035);
            int k = 0;
            P(ref k, a, v);
            P(ref k, b, v);
            P(ref k, b, v + h - r);
            P(ref k, b - r, v + h);
            P(ref k, a + r, v + h);
            P(ref k, a, v + h - r);
            P(ref k, a, v);
            return SweepU(m, f, u0, u1, k, caps, c, 50);
        }

        private static void P(ref int k, double w, double v)
        {
            _pw[k] = w;
            _pv[k] = v;
            k++;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Infill
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A diagonal (±45°) lattice of strips of width <paramref name="bar"/> at <paramref name="pitch"/> spacing
        /// over [u0, u1] × [v0, v1] in the plane w, each strip a front face plus a thin lower bevel (4 triangles), clipped
        /// to the rectangle: the Newar tikijhya and sanjhya screen. <paramref name="square"/> draws a square grid
        /// instead. Returns the triangles added.
        /// </summary>
        public static int Lattice(MeshData m, in KitFrame f, double u0, double v0, double u1, double v1, double w, double pitch, double bar,
                                  double thick, bool square, uint c)
        {
            return Lattice(m, f, u0, v0, u1, v1, w, pitch, bar, thick, square, c, 0);
        }

        /// <summary>As the other overload; <paramref name="phase"/> (0..1) shifts the lines by a share of the pitch so
        /// neighbouring screens do not line up.</summary>
        public static int Lattice(MeshData m, in KitFrame f, double u0, double v0, double u1, double v1, double w, double pitch, double bar,
                                  double thick, bool square, uint c, double phase)
        {
            if (u1 - u0 < 0.05 || v1 - v0 < 0.05 || pitch < 0.02) return 0;
            int tris = 0;
            double hb = 0.5 * bar;
            if (square)
            {
                for (double u = u0 + pitch * 0.5; u < u1 - 0.25 * pitch; u += pitch)
                    tris += Strip(m, f, u - hb, v0, u + hb, v1, w, thick, true, c);
                for (double v = v0 + pitch * 0.5; v < v1 - 0.25 * pitch; v += pitch)
                    tris += Strip(m, f, u0, v - hb, u1, v + hb, w, thick, false, c);
                return tris;
            }
            double w0 = u1 - u0, h0 = v1 - v0;
            // Lines u - v = k (rising) and u + v = k (falling), clipped to the box.
            for (int dir = 0; dir < 2; dir++)
            {
                double kmin = dir == 0 ? -h0 : 0, kmax = dir == 0 ? w0 : w0 + h0;
                double start = kmin + (pitch - ((kmin % pitch) + pitch) % pitch) % pitch + (0.5 + phase) * pitch;
                for (double kk = start; kk < kmax; kk += pitch)
                {
                    // Parametrise the line by local u: rising v = u - kk, falling v = kk - u.
                    double ua, ub;
                    if (dir == 0)
                    {
                        ua = Math.Max(0, kk);
                        ub = Math.Min(w0, kk + h0);
                    }
                    else
                    {
                        ua = Math.Max(0, kk - h0);
                        ub = Math.Min(w0, kk);
                    }
                    if (ub - ua < 0.02) continue;
                    double va = dir == 0 ? ua - kk : kk - ua, vb = dir == 0 ? ub - kk : kk - ub;
                    tris += Diagonal(m, f, u0 + ua, v0 + va, u0 + ub, v0 + vb, w, bar, thick, c);
                }
            }
            return tris;
        }

        /// <summary>A straight strip from (ua, va) to (ub, vb) in plane w: front face and its lower side face.</summary>
        private static int Diagonal(MeshData m, in KitFrame f, double ua, double va, double ub, double vb, double w, double bar, double thick, uint c)
        {
            double du = ub - ua, dv = vb - va, l = Math.Sqrt(du * du + dv * dv);
            if (l < 1e-5) return 0;
            // Perpendicular in the (u, v) plane, half width.
            double pu = -dv / l * 0.5 * bar, pv = du / l * 0.5 * bar;
            if (pv < 0)
            {
                pu = -pu;
                pv = -pv;
            }
            int t = MeshKit.QuadLocal(m, f, ua - pu, va - pv, w, ub - pu, vb - pv, w, ub + pu, vb + pv, w, ua + pu, va + pv, w, 0, 0, 1, c);
            if (thick > 0)
                t += MeshKit.QuadLocal(m, f, ua - pu, va - pv, w, ub - pu, vb - pv, w, ub - pu, vb - pv, w - thick, ua - pu, va - pv, w - thick,
                                       -pu, -pv, 0.2, c);
            return t;
        }

        private static int Strip(MeshData m, in KitFrame f, double u0, double v0, double u1, double v1, double w, double thick, bool vertical, uint c)
        {
            int t = MeshKit.Panel(m, f, u0, v0, u1, v1, w, c);
            if (thick > 0)
            {
                if (vertical) t += MeshKit.QuadLocal(m, f, u0, v0, w, u0, v1, w, u0, v1, w - thick, u0, v0, w - thick, -1, 0, 0, c);
                else t += MeshKit.QuadLocal(m, f, u0, v0, w, u1, v0, w, u1, v0, w - thick, u0, v0, w - thick, 0, -1, 0, c);
            }
            return t;
        }

        /// <summary>
        /// A corrugated panel in the plane w over [u0, u1] × [v0, v1] with horizontal ribs every
        /// <paramref name="pitch"/> metres, <paramref name="depth"/> deep, smooth-shaded (a roller shutter or a CGI
        /// sheet on its side). Returns the triangles added.
        /// </summary>
        public static int Corrugated(MeshData m, in KitFrame f, double u0, double v0, double u1, double v1, double w, double pitch, double depth, uint c)
        {
            if (u1 - u0 < 1e-3 || v1 - v0 < 1e-3) return 0;
            int ribs = Math.Max(1, (int)Math.Round((v1 - v0) / Math.Max(0.02, pitch)));
            int pts = 2 * ribs + 1;
            Profile(pts + 2);
            double step = (v1 - v0) / (pts - 1);
            int k = 0;
            for (int i = 0; i < pts; i++) P(ref k, w + ((i & 1) == 1 ? depth : 0), v0 + step * i);
            return SweepU(m, f, u0, u1, k, 0, c, 89);
        }

        /// <summary>A straight rod in frame coordinates (radius r, <paramref name="sides"/> sides).</summary>
        public static int RodLocal(MeshData m, in KitFrame f, double ua, double va, double wa, double ub, double vb, double wb, double r, int sides,
                                   bool caps, uint c)
        {
            double ax, ay, az, bx, by, bz;
            f.ToWorld(ua, va, wa, out ax, out ay, out az);
            f.ToWorld(ub, vb, wb, out bx, out by, out bz);
            return KitRound.Rod(m, ax, ay, az, bx, by, bz, r, sides, caps, c);
        }
    }
}
