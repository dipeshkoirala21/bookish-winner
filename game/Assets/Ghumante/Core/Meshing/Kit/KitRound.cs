using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Smooth-shaded kit primitives for the building grammar (docs/W2_DETAIL_CONTRACT.md decision 6: rounded edges and
    /// curved surfaces instead of boxes): rounded-rectangle prisms (bevelled slabs, bands, posts, boards), lathes
    /// (tanks, pots, columns), tubes along polylines (railings, rebar, struts, lines), ellipsoids (foliage, knobs) and
    /// quads with per-vertex normals (bevelled wall corners). Normals are smooth across the curved parts and hard at
    /// the flat sides, so the toon ramp reads a rounded edge. Triangles are wound to face their vertex normals
    /// (Unity winding, as <see cref="MeshKit"/>). Colours as in <see cref="MeshData"/>; channels and AO come from a
    /// <see cref="KitPaint"/> pass. Nothing allocates beyond <see cref="MeshData"/> growth (thread-static scratch).
    /// </summary>
    public static class KitRound
    {
        [ThreadStatic] private static double[] _sb, _sc, _snb, _snc;
        [ThreadStatic] private static int[] _sk;

        private static void Section(int n)
        {
            if (_sb != null && _sb.Length >= n) return;
            int cap = Math.Max(n, 64);
            _sb = new double[cap];
            _sc = new double[cap];
            _snb = new double[cap];
            _snc = new double[cap];
            _sk = new int[cap];
        }

        /// <summary>Add triangle (a, b, c) of existing vertices wound so its front faces along the sum of their
        /// normals. Degenerate triangles are skipped. Returns the triangles added.</summary>
        public static int TriOriented(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions, n = m.Normals;
            int ia = a * 3, ib = b * 3, ic = c * 3;
            double ux = p[ib] - p[ia], uy = p[ib + 1] - p[ia + 1], uz = p[ib + 2] - p[ia + 2];
            double vx = p[ic] - p[ia], vy = p[ic + 1] - p[ia + 1], vz = p[ic + 2] - p[ia + 2];
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            if (cx * cx + cy * cy + cz * cz < 1e-14) return 0;
            double nx = n[ia] + n[ib] + n[ic], ny = n[ia + 1] + n[ib + 1] + n[ic + 1], nz = n[ia + 2] + n[ib + 2] + n[ic + 2];
            if (cx * nx + cy * ny + cz * nz >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
            return 1;
        }

        private static int V(MeshData m, double x, double y, double z, double nx, double ny, double nz, uint c)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-12)
            {
                nx = 0;
                ny = 1;
                nz = 0;
                l = 1;
            }
            return m.AddVertex((float)x, (float)y, (float)z, (float)(nx / l), (float)(ny / l), (float)(nz / l), c);
        }

        /// <summary>A quad (p0..p3 around its perimeter) with one normal per corner.</summary>
        public static int QuadSmooth(MeshData m, double x0, double y0, double z0, double x1, double y1, double z1,
                                     double x2, double y2, double z2, double x3, double y3, double z3,
                                     double n0x, double n0y, double n0z, double n1x, double n1y, double n1z,
                                     double n2x, double n2y, double n2z, double n3x, double n3y, double n3z, uint c)
        {
            m.Reserve(4, 6);
            int v = V(m, x0, y0, z0, n0x, n0y, n0z, c);
            V(m, x1, y1, z1, n1x, n1y, n1z, c);
            V(m, x2, y2, z2, n2x, n2y, n2z, c);
            V(m, x3, y3, z3, n3x, n3y, n3z, c);
            return TriOriented(m, v, v + 1, v + 2) + TriOriented(m, v, v + 2, v + 3);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Rounded-rectangle prisms
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Section side bits of <see cref="Prism"/>: the c1, b0, c0 and b1 sides of the rectangle.</summary>
        public const int SideC1 = 1, SideB0 = 2, SideC0 = 4, SideB1 = 8, SidesAll = 15;

        /// <summary>
        /// A prism along the unit axis A from origin O for <paramref name="len"/> metres. Its section is the rectangle
        /// [b0, b1] × [c0, c1] in the plane of the unit axes B and C, with corners rounded to radius
        /// <paramref name="r"/> in <paramref name="segs"/> segments (0 = square). <paramref name="sides"/> picks the
        /// section sides drawn (<see cref="SideC1"/>...); a corner next to a missing side stays square. Caps: bit 1 the
        /// start, bit 2 the end. Returns the triangles added.
        /// </summary>
        public static int Prism(MeshData m, double ox, double oy, double oz, double ax, double ay, double az, double len,
                                double bx, double by, double bz, double cx, double cy, double cz,
                                double b0, double b1, double c0, double c1, double r, int segs, int sides, int caps, uint col)
        {
            if (len <= 1e-6 || b1 - b0 <= 1e-6 || c1 - c0 <= 1e-6) return 0;
            r = Math.Max(0, Math.Min(r, 0.5 * Math.Min(b1 - b0, c1 - c0) - 1e-4));
            if (r <= 1e-5) segs = 0;
            Section(4 * (segs + 2) + 4);
            // Walk the section: side c1 (towards -b), corner (b0, c1), side b0 (towards -c), corner (b0, c0), side c0,
            // corner (b1, c0), side b1, corner (b1, c1). Corner k sits between side k and side k+1 (mod 4).
            int[] sideBit = { SideC1, SideB0, SideC0, SideB1 };
            double[] cornerB = { b0, b0, b1, b1 }, cornerC = { c1, c0, c0, c1 };
            double[] sideNb = { 0, -1, 0, 1 }, sideNc = { 1, 0, -1, 0 };
            int k = 0;
            // Each point records which "kind" of strip follows it: -1 corner arc point, else the side index.
            for (int corner = 0; corner < 4; corner++)
            {
                int sIn = corner, sOut = (corner + 1) & 3;
                bool round = segs > 0 && (sides & sideBit[sIn]) != 0 && (sides & sideBit[sOut]) != 0;
                double cb = cornerB[corner], cc = cornerC[corner];
                if (!round)
                {
                    _sb[k] = cb;
                    _sc[k] = cc;
                    _snb[k] = sideNb[sOut];
                    _snc[k] = sideNc[sOut];
                    _sk[k] = sOut;
                    k++;
                    continue;
                }
                // Arc centre inset by r; normal turns from side sIn's to side sOut's.
                double ccb = cb - sideNb[sIn] * r - sideNb[sOut] * r, ccc = cc - sideNc[sIn] * r - sideNc[sOut] * r;
                double a0 = Math.Atan2(sideNc[sIn], sideNb[sIn]), a1 = Math.Atan2(sideNc[sOut], sideNb[sOut]);
                double da = a1 - a0;
                while (da > Math.PI) da -= 2 * Math.PI;
                while (da < -Math.PI) da += 2 * Math.PI;
                for (int s = 0; s <= segs; s++)
                {
                    double a = a0 + da * s / segs, nb = Math.Cos(a), nc = Math.Sin(a);
                    _sb[k] = ccb + r * nb;
                    _sc[k] = ccc + r * nc;
                    _snb[k] = nb;
                    _snc[k] = nc;
                    _sk[k] = s < segs ? -1 : sOut;
                    k++;
                }
            }
            int count = k, tris = 0;
            m.Reserve(count * 4 + 2 * count, count * 6 + 6 * count);
            for (int i = 0; i < count; i++)
            {
                int j = i + 1 == count ? 0 : i + 1;
                int kind = _sk[i];
                if (kind >= 0 && (sides & sideBit[kind]) == 0) continue;
                double nbi, nci, nbj, ncj;
                if (kind < 0)
                {
                    nbi = _snb[i];
                    nci = _snc[i];
                    nbj = _snb[j];
                    ncj = _snc[j];
                }
                else
                {
                    nbi = nbj = sideNb[kind];
                    nci = ncj = sideNc[kind];
                }
                double pix = ox + bx * _sb[i] + cx * _sc[i], piy = oy + by * _sb[i] + cy * _sc[i], piz = oz + bz * _sb[i] + cz * _sc[i];
                double pjx = ox + bx * _sb[j] + cx * _sc[j], pjy = oy + by * _sb[j] + cy * _sc[j], pjz = oz + bz * _sb[j] + cz * _sc[j];
                double nix = bx * nbi + cx * nci, niy = by * nbi + cy * nci, niz = bz * nbi + cz * nci;
                double njx = bx * nbj + cx * ncj, njy = by * nbj + cy * ncj, njz = bz * nbj + cz * ncj;
                tris += QuadSmooth(m, pix, piy, piz, pjx, pjy, pjz, pjx + ax * len, pjy + ay * len, pjz + az * len,
                                   pix + ax * len, piy + ay * len, piz + az * len, nix, niy, niz, njx, njy, njz, njx, njy, njz, nix, niy, niz, col);
            }
            for (int e = 0; e < 2; e++)
            {
                if ((caps & (1 << e)) == 0) continue;
                double off = e == 0 ? 0 : len, s = e == 0 ? -1 : 1;
                int v0 = m.VertexCount;
                for (int i = 0; i < count; i++)
                    V(m, ox + bx * _sb[i] + cx * _sc[i] + ax * off, oy + by * _sb[i] + cy * _sc[i] + ay * off,
                      oz + bz * _sb[i] + cz * _sc[i] + az * off, ax * s, ay * s, az * s, col);
                for (int i = 1; i + 1 < count; i++) tris += TriOriented(m, v0, v0 + i, v0 + i + 1);
            }
            return tris;
        }

        /// <summary>A rounded box in a facade frame extruded along U over [u0, u1]: section w × v with radius r.
        /// Faces: Top, Bottom, Front (+W), Back (−W) pick the section sides; Left and Right the end caps.</summary>
        public static int BoxU(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w0, double w1,
                               double r, int segs, BoxFaces faces, uint c)
        {
            double ox, oy, oz;
            f.ToWorld(u0, 0, 0, out ox, out oy, out oz);
            int sides = ((faces & BoxFaces.Top) != 0 ? SideC1 : 0) | ((faces & BoxFaces.Bottom) != 0 ? SideC0 : 0) |
                        ((faces & BoxFaces.Front) != 0 ? SideB1 : 0) | ((faces & BoxFaces.Back) != 0 ? SideB0 : 0);
            int caps = ((faces & BoxFaces.Left) != 0 ? 1 : 0) | ((faces & BoxFaces.Right) != 0 ? 2 : 0);
            return Prism(m, ox, oy, oz, f.UX, 0, f.UZ, u1 - u0, f.WX, 0, f.WZ, 0, 1, 0, w0, w1, v0, v1, r, segs, sides, caps, c);
        }

        /// <summary>A rounded post in a facade frame extruded up over [v0, v1]: section u × w with radius r. Faces:
        /// Front, Back, Left, Right pick the section sides; Bottom and Top the end caps.</summary>
        public static int BoxV(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w0, double w1,
                               double r, int segs, BoxFaces faces, uint c)
        {
            double ox, oy, oz;
            f.ToWorld(0, v0, 0, out ox, out oy, out oz);
            int sides = ((faces & BoxFaces.Front) != 0 ? SideC1 : 0) | ((faces & BoxFaces.Back) != 0 ? SideC0 : 0) |
                        ((faces & BoxFaces.Left) != 0 ? SideB0 : 0) | ((faces & BoxFaces.Right) != 0 ? SideB1 : 0);
            int caps = ((faces & BoxFaces.Bottom) != 0 ? 1 : 0) | ((faces & BoxFaces.Top) != 0 ? 2 : 0);
            return Prism(m, ox, oy, oz, 0, 1, 0, v1 - v0, f.UX, 0, f.UZ, f.WX, 0, f.WZ, u0, u1, w0, w1, r, segs, sides, caps, c);
        }

        /// <summary>A rounded beam in a facade frame extruded out along W over [w0, w1]: section u × v with radius r.
        /// Faces: Top, Bottom, Left, Right pick the section sides; Back and Front the end caps.</summary>
        public static int BoxW(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w0, double w1,
                               double r, int segs, BoxFaces faces, uint c)
        {
            double ox, oy, oz;
            f.ToWorld(0, 0, w0, out ox, out oy, out oz);
            int sides = ((faces & BoxFaces.Top) != 0 ? SideC1 : 0) | ((faces & BoxFaces.Bottom) != 0 ? SideC0 : 0) |
                        ((faces & BoxFaces.Left) != 0 ? SideB0 : 0) | ((faces & BoxFaces.Right) != 0 ? SideB1 : 0);
            int caps = ((faces & BoxFaces.Back) != 0 ? 1 : 0) | ((faces & BoxFaces.Front) != 0 ? 2 : 0);
            return Prism(m, ox, oy, oz, f.WX, 0, f.WZ, w1 - w0, f.UX, 0, f.UZ, 0, 1, 0, u0, u1, v0, v1, r, segs, sides, caps, c);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lathes and ellipsoids
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A surface of revolution about the vertical axis through (cx, cz): profile points (r[k], y[k]) relative to
        /// (cx, cy, cz), listed from the bottom up with the outside on the right (normal = (dy, −dr)), e.g. bottom centre
        /// → rim → up the wall → top centre. Normals are smooth around and along the profile, hard where it turns more
        /// than <paramref name="hardDeg"/>. Points on the axis close the surface. Returns the triangles added.
        /// </summary>
        public static int Lathe(MeshData m, double cx, double cy, double cz, double[] r, double[] y, int count, int sides, uint c,
                                double hardDeg = 40, double phase = 0)
        {
            if (count < 2 || sides < 3) return 0;
            double cosHard = Math.Cos(hardDeg * Math.PI / 180.0);
            int tris = 0;
            m.Reserve(2 * sides * (count - 1), 6 * sides * (count - 1));
            for (int s = 0; s + 1 < count; s++)
            {
                double dr = r[s + 1] - r[s], dy = y[s + 1] - y[s];
                double sl = Math.Sqrt(dr * dr + dy * dy);
                if (sl < 1e-9) continue;
                double snr = dy / sl, sny = -dr / sl;
                double n0r, n0y, n1r, n1y;
                Joint(r, y, count, s, s, snr, sny, cosHard, out n0r, out n0y);
                Joint(r, y, count, s + 1, s, snr, sny, cosHard, out n1r, out n1y);
                bool pole0 = r[s] < 1e-7, pole1 = r[s + 1] < 1e-7;
                if (pole0 && pole1) continue;
                int v0 = m.VertexCount;
                for (int j = 0; j < sides; j++)
                {
                    double a = phase + 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                    V(m, cx + r[s] * ca, cy + y[s], cz + r[s] * sa, n0r * ca, n0y, n0r * sa, c);
                    V(m, cx + r[s + 1] * ca, cy + y[s + 1], cz + r[s + 1] * sa, n1r * ca, n1y, n1r * sa, c);
                }
                for (int j = 0; j < sides; j++)
                {
                    int jn = j + 1 == sides ? 0 : j + 1;
                    int a0 = v0 + 2 * j, a1 = v0 + 2 * j + 1, b0 = v0 + 2 * jn, b1 = v0 + 2 * jn + 1;
                    if (!pole0) tris += TriOriented(m, a0, b0, b1);
                    if (!pole1) tris += TriOriented(m, a0, b1, a1);
                }
            }
            return tris;
        }

        /// <summary>Normal (radial, up) at profile point k for segment s: the average with the neighbouring segment
        /// when the turn is gentle, else the segment's own.</summary>
        private static void Joint(double[] r, double[] y, int count, int k, int s, double snr, double sny, double cosHard,
                                  out double nr, out double ny)
        {
            nr = snr;
            ny = sny;
            int o = k == s ? s - 1 : s + 1; // the other segment at point k
            if (o < 0 || o + 1 >= count) return;
            double dr = r[o + 1] - r[o], dy = y[o + 1] - y[o], l = Math.Sqrt(dr * dr + dy * dy);
            if (l < 1e-9) return;
            double onr = dy / l, ony = -dr / l;
            if (onr * snr + ony * sny < cosHard) return;
            nr = snr + onr;
            ny = sny + ony;
            double nl = Math.Sqrt(nr * nr + ny * ny);
            if (nl < 1e-9)
            {
                nr = snr;
                ny = sny;
                return;
            }
            nr /= nl;
            ny /= nl;
        }

        /// <summary>A vertical smooth cylinder (optionally capped top and bottom).</summary>
        public static int Cylinder(MeshData m, double cx, double cz, double rad, double y0, double y1, int sides, bool top, bool bottom, uint c)
        {
            Profile(4);
            int k = 0;
            if (bottom) Add(ref k, 0, 0);
            Add(ref k, rad, 0);
            Add(ref k, rad, y1 - y0);
            if (top) Add(ref k, 0, y1 - y0);
            return Lathe(m, cx, y0, cz, _pr, _py, k, sides, c);
        }

        [ThreadStatic] private static double[] _pr, _py;

        private static void Profile(int n)
        {
            if (_pr != null && _pr.Length >= n) return;
            _pr = new double[Math.Max(n, 32)];
            _py = new double[Math.Max(n, 32)];
        }

        private static void Add(ref int k, double r, double y)
        {
            _pr[k] = r;
            _py[k] = y;
            k++;
        }

        /// <summary>The thread's profile scratch for <see cref="LatheScratch"/>: fill <c>R</c> and <c>Y</c>, then call
        /// it with the count.</summary>
        public static double[] ProfileR
        {
            get
            {
                Profile(32);
                return _pr;
            }
        }

        public static double[] ProfileY
        {
            get
            {
                Profile(32);
                return _py;
            }
        }

        /// <summary>A lathe over the first <paramref name="count"/> points of <see cref="ProfileR"/> / <see cref="ProfileY"/>.</summary>
        public static int LatheScratch(MeshData m, double cx, double cy, double cz, int count, int sides, uint c, double hardDeg = 40)
        {
            return Lathe(m, cx, cy, cz, _pr, _py, count, sides, c, hardDeg);
        }

        /// <summary>An ellipsoid with radii (rx, ry, rz) about (cx, cy, cz): <paramref name="sides"/> around,
        /// <paramref name="bands"/> from pole to pole, smooth normals.</summary>
        public static int Ellipsoid(MeshData m, double cx, double cy, double cz, double rx, double ry, double rz, int sides, int bands, uint c)
        {
            if (sides < 3 || bands < 2) return 0;
            m.Reserve(sides * (bands - 1) + 2, 6 * sides * bands);
            int bottom = V(m, cx, cy - ry, cz, 0, -1, 0, c);
            int ring0 = m.VertexCount;
            for (int b = 1; b < bands; b++)
            {
                double phi = Math.PI * b / bands - 0.5 * Math.PI, cp = Math.Cos(phi), sp = Math.Sin(phi);
                for (int j = 0; j < sides; j++)
                {
                    double a = 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                    double x = rx * cp * ca, yy = ry * sp, z = rz * cp * sa;
                    V(m, cx + x, cy + yy, cz + z, x / (rx * rx), yy / (ry * ry), z / (rz * rz), c);
                }
            }
            int top = V(m, cx, cy + ry, cz, 0, 1, 0, c);
            int tris = 0;
            for (int j = 0; j < sides; j++)
            {
                int jn = j + 1 == sides ? 0 : j + 1;
                tris += TriOriented(m, bottom, ring0 + j, ring0 + jn);
                for (int b = 0; b + 2 < bands; b++)
                {
                    int a0 = ring0 + b * sides + j, a1 = ring0 + b * sides + jn, b0 = a0 + sides, b1 = a1 + sides;
                    tris += TriOriented(m, a0, a1, b1);
                    tris += TriOriented(m, a0, b1, b0);
                }
                int last = ring0 + (bands - 2) * sides;
                tris += TriOriented(m, top, last + j, last + jn);
            }
            return tris;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Tubes
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A tube of <paramref name="sides"/> sides along a polyline of <paramref name="n"/> points (radius per point,
        /// or <paramref name="radius"/> when <paramref name="radii"/> is null), with frames carried along the path
        /// (no twist) and smooth normals around; optional flat end caps. Returns the triangles added.
        /// </summary>
        public static int Tube(MeshData m, double[] px, double[] py, double[] pz, double[] radii, double radius, int n, int sides, bool caps, uint c)
        {
            if (n < 2 || sides < 3) return 0;
            // Initial frame: tangent t0, normal u perpendicular to it.
            double tx, ty, tz;
            Tangent(px, py, pz, n, 0, out tx, out ty, out tz);
            double ux, uy, uz;
            if (Math.Abs(ty) < 0.9)
            {
                ux = -tz;
                uy = 0;
                uz = tx;
            }
            else
            {
                ux = 1;
                uy = 0;
                uz = 0;
            }
            Norm(ref ux, ref uy, ref uz);
            int tris = 0;
            int prev = -1;
            m.Reserve(n * sides + 2 * sides, 6 * sides * n);
            for (int k = 0; k < n; k++)
            {
                Tangent(px, py, pz, n, k, out tx, out ty, out tz);
                // Carry u: remove its component along the new tangent.
                double d = ux * tx + uy * ty + uz * tz;
                ux -= d * tx;
                uy -= d * ty;
                uz -= d * tz;
                if (!Norm(ref ux, ref uy, ref uz))
                {
                    ux = -tz;
                    uy = 0;
                    uz = tx;
                    if (!Norm(ref ux, ref uy, ref uz))
                    {
                        ux = 1;
                        uy = 0;
                        uz = 0;
                    }
                }
                double vx = ty * uz - tz * uy, vy = tz * ux - tx * uz, vz = tx * uy - ty * ux;
                double rad = radii != null ? radii[k] : radius;
                int ring = m.VertexCount;
                for (int j = 0; j < sides; j++)
                {
                    double a = 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                    double nx = ux * ca + vx * sa, ny = uy * ca + vy * sa, nz = uz * ca + vz * sa;
                    V(m, px[k] + rad * nx, py[k] + rad * ny, pz[k] + rad * nz, nx, ny, nz, c);
                }
                if (prev >= 0)
                    for (int j = 0; j < sides; j++)
                    {
                        int jn = j + 1 == sides ? 0 : j + 1;
                        tris += TriOriented(m, prev + j, prev + jn, ring + jn);
                        tris += TriOriented(m, prev + j, ring + jn, ring + j);
                    }
                prev = ring;
                if (caps && (k == 0 || k == n - 1))
                {
                    double s = k == 0 ? -1 : 1;
                    int c0 = m.VertexCount;
                    for (int j = 0; j < sides; j++)
                    {
                        double a = 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                        double ox = ux * ca + vx * sa, oy = uy * ca + vy * sa, oz = uz * ca + vz * sa;
                        V(m, px[k] + rad * ox, py[k] + rad * oy, pz[k] + rad * oz, tx * s, ty * s, tz * s, c);
                    }
                    for (int j = 1; j + 1 < sides; j++) tris += TriOriented(m, c0, c0 + j, c0 + j + 1);
                }
            }
            return tris;
        }

        [ThreadStatic] private static double[] _tx, _ty, _tz;

        /// <summary>A straight smooth rod from a to b (radius r, <paramref name="sides"/> sides, capped).</summary>
        public static int Rod(MeshData m, double ax, double ay, double az, double bx, double by, double bz, double r, int sides, bool caps, uint c)
        {
            if (_tx == null)
            {
                _tx = new double[8];
                _ty = new double[8];
                _tz = new double[8];
            }
            _tx[0] = ax;
            _ty[0] = ay;
            _tz[0] = az;
            _tx[1] = bx;
            _ty[1] = by;
            _tz[1] = bz;
            return Tube(m, _tx, _ty, _tz, null, r, 2, sides, caps, c);
        }

        private static void Tangent(double[] px, double[] py, double[] pz, int n, int k, out double tx, out double ty, out double tz)
        {
            int a = k == 0 ? 0 : k - 1, b = k == n - 1 ? n - 1 : k + 1;
            tx = px[b] - px[a];
            ty = py[b] - py[a];
            tz = pz[b] - pz[a];
            if (!Norm(ref tx, ref ty, ref tz))
            {
                tx = 0;
                ty = 1;
                tz = 0;
            }
        }

        private static bool Norm(ref double x, ref double y, ref double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            if (l < 1e-12) return false;
            x /= l;
            y /= l;
            z /= l;
            return true;
        }
    }
}
