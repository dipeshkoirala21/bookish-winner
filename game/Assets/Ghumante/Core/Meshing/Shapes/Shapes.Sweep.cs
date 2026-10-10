using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>Edge style of <see cref="Shapes.BevelExtrude"/>.</summary>
    public enum BevelStyle : byte
    {
        /// <summary>Quarter-round edges with smooth shading.</summary>
        Round = 0,

        /// <summary>One flat 45° facet per edge with hard shading.</summary>
        Chamfer = 1,
    }

    /// <summary>How <see cref="Shapes.Sweep"/> orients its cross-sections.</summary>
    public enum SweepFrames : byte
    {
        /// <summary>Rotation-minimising frames (no twist along curly paths: pipes, wires, handlebars, tails).</summary>
        ParallelTransport = 0,

        /// <summary>Profile +Y stays as close to world up as possible (railings, kerbs, mouldings, beams).</summary>
        Upright = 1,
    }

    public static partial class Shapes
    {
        [ThreadStatic] private static Path3 s_path;
        [ThreadStatic] private static Profile2 s_circle;

        // ------------------------------------------------------------------ bevel extrude

        /// <summary>
        /// Extrude a simple polygon (x, z arrays, either orientation) from y = 0 up to <paramref name="height"/> with
        /// bevelled edges: the top edge rounds (or chamfers) by <paramref name="bevel"/>, the bottom edge by
        /// <paramref name="bottomBevel"/>, both with <paramref name="bevelSegments"/> segments. Polygon corners
        /// sharper than <paramref name="creaseDeg"/> stay hard; gentler ones (an arc from
        /// <see cref="Profile2.FilletCorners"/>) shade smoothly. Optional top and bottom caps (ear-clipped).
        /// Plinths, slabs, steps, table tops, window frames, signboards, cushions.
        /// </summary>
        public static int BevelExtrude(MeshData m, in Affine3 xf, in ShapeBrush b, double[] x, double[] z, int n, double height,
                                       double bevel, int bevelSegments, BevelStyle style = BevelStyle.Round, bool capTop = true,
                                       bool capBottom = true, double bottomBevel = 0, double creaseDeg = 30, ShapeLod lod = default)
        {
            int first = m.VertexCount;
            // Distinct, counter-clockwise copy of the polygon.
            double[] px = ShapeEmit.Scratch(0, n + 1), pz = ShapeEmit.Scratch(1, n + 1);
            int c = 0;
            for (int i = 0; i < n; i++)
            {
                if (c > 0 && Math.Abs(x[i] - px[c - 1]) < 1e-9 && Math.Abs(z[i] - pz[c - 1]) < 1e-9) continue;
                px[c] = x[i];
                pz[c] = z[i];
                c++;
            }
            if (c > 1 && Math.Abs(px[0] - px[c - 1]) < 1e-9 && Math.Abs(pz[0] - pz[c - 1]) < 1e-9) c--;
            if (c < 3 || height <= 0) return first;
            if (Polygon.SignedArea(px, pz, c) < 0)
            {
                for (int i = 0, j = c - 1; i < j; i++, j--)
                {
                    double t = px[i];
                    px[i] = px[j];
                    px[j] = t;
                    t = pz[i];
                    pz[i] = pz[j];
                    pz[j] = t;
                }
            }
            double bt = Math.Max(0, bevel), bb = Math.Max(0, bottomBevel);
            if (bt + bb > height * 0.999)
            {
                double f = height * 0.999 / (bt + bb);
                bt *= f;
                bb *= f;
            }
            // Ring path: per corner the miter vector (offset per metre of inset) and the shading normal(s).
            double[] mx = ShapeEmit.Scratch(2, c), mz = ShapeEmit.Scratch(3, c);
            RingPath ring = ShapeEmit.Ring;
            ring.Clear();
            double cosCrease = Math.Cos(creaseDeg * Math.PI / 180.0);
            for (int i = 0; i < c; i++)
            {
                int ip = (i + c - 1) % c, inx = (i + 1) % c;
                double n1x, n1z, n2x, n2z;
                EdgeOut(px[ip], pz[ip], px[i], pz[i], out n1x, out n1z);
                EdgeOut(px[i], pz[i], px[inx], pz[inx], out n2x, out n2z);
                double dot = n1x * n2x + n1z * n2z;
                double den = Math.Max(1 + dot, 0.125);
                mx[i] = (n1x + n2x) / den;
                mz[i] = (n1z + n2z) / den;
                if (dot < cosCrease)
                {
                    ring.Add(px[i], pz[i], mx[i], mz[i], n1x, n1z, true);
                    ring.Add(px[i], pz[i], mx[i], mz[i], n2x, n2z, false);
                }
                else
                {
                    double sx = n1x + n2x, sz = n1z + n2z, l = Math.Sqrt(sx * sx + sz * sz);
                    ring.Add(px[i], pz[i], mx[i], mz[i], sx / l, sz / l, false);
                }
            }
            // Vertical section (offset, y): outward on the right going up.
            int seg = lod.Bevel(Math.Max(1, bevelSegments));
            Profile2 sec = TempProfile.Clear(false);
            if (bb > 1e-9)
            {
                if (style == BevelStyle.Chamfer)
                {
                    sec.Add(-bb, 0, true).Add(0, bb, true);
                }
                else
                {
                    for (int k = 0; k <= seg; k++)
                    {
                        double th = -0.5 * Math.PI + 0.5 * Math.PI * k / seg;
                        double co = k == 0 ? 0 : k == seg ? 1 : Math.Cos(th), si = k == 0 ? -1 : k == seg ? 0 : Math.Sin(th);
                        sec.Add(-bb * (1 - co), bb + bb * si, co, si, false);
                    }
                }
            }
            else sec.Add(0, 0, true);
            if (bt > 1e-9)
            {
                if (style == BevelStyle.Chamfer)
                {
                    sec.Add(0, height - bt, true).Add(-bt, height, true);
                }
                else
                {
                    for (int k = 0; k <= seg; k++)
                    {
                        double th = 0.5 * Math.PI * k / seg;
                        double co = k == 0 ? 1 : k == seg ? 0 : Math.Cos(th), si = k == 0 ? 0 : k == seg ? 1 : Math.Sin(th);
                        sec.Add(-bt * (1 - co), height - bt + bt * si, co, si, false);
                    }
                }
            }
            else sec.Add(0, height, true);
            if (bb <= 1e-9) sec.Crease[0] = true;
            if (bt <= 1e-9) sec.Crease[sec.Count - 1] = true;
            // The straight wall between the two bevels is a crease for chamfers only; round bevels are tangent.
            Expanded e = ShapeEmit.ExpA;
            int s = ShapeEmit.Expand(sec, e);
            m.Reserve(ring.Count * s, ring.Count * s * 6);
            for (int r = 0; r < ring.Count; r++)
            {
                for (int k = 0; k < s; k++)
                {
                    double off = e.X[k];
                    double nx = ring.NX[r] * e.NX[k], ny = e.NY[k], nz = ring.NZ[r] * e.NX[k];
                    ShapeEmit.Normalize(ref nx, ref ny, ref nz);
                    ShapeEmit.Vertex(m, xf, b, ring.X[r] + ring.MX[r] * off, e.Y[k], ring.Z[r] + ring.MZ[r] * off, nx, ny, nz);
                }
            }
            ShapeEmit.Grid(m, first, ring.Count, s, false, true, e.Seam, ring.Seam);
            if (capTop) ExtrudeCap(m, xf, b, px, pz, mx, mz, c, -bt, height, 1);
            if (capBottom) ExtrudeCap(m, xf, b, px, pz, mx, mz, c, -bb, 0, -1);
            return first;
        }

        private static void EdgeOut(double ax, double az, double bx, double bz, out double nx, out double nz)
        {
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-12)
            {
                nx = 0;
                nz = 0;
                return;
            }
            // Counter-clockwise polygon (x east, z north): outward is to the right of travel.
            nx = dz / l;
            nz = -dx / l;
        }

        private static void ExtrudeCap(MeshData m, in Affine3 xf, in ShapeBrush b, double[] px, double[] pz, double[] mx, double[] mz,
                                       int c, double off, double y, double ny)
        {
            double[] cx = ShapeEmit.Scratch(4, c), cz = ShapeEmit.Scratch(5, c);
            for (int i = 0; i < c; i++)
            {
                cx[i] = px[i] + mx[i] * off;
                cz[i] = pz[i] + mz[i] * off;
            }
            int[] tris, idx;
            int pc;
            int tc = ShapeEmit.TriangulateArrays(cx, cz, c, out tris, out idx, out pc);
            int v0 = m.VertexCount;
            m.Reserve(pc, tc * 3);
            for (int k = 0; k < pc; k++) ShapeEmit.Vertex(m, xf, b, cx[idx[k]], y, cz[idx[k]], 0, ny, 0);
            for (int t = 0; t < tc; t++) ShapeEmit.TriOriented(m, v0 + tris[3 * t], v0 + tris[3 * t + 1], v0 + tris[3 * t + 2]);
        }

        /// <summary>A rectangular slab sx × sz (centred on the origin in plan) standing on y = 0 to
        /// <paramref name="height"/>, plan corners rounded by <paramref name="cornerRadius"/> and the top edge
        /// bevelled (see <see cref="BevelExtrude"/>).</summary>
        public static int RoundedSlab(MeshData m, in Affine3 xf, in ShapeBrush b, double sx, double sz, double height, double cornerRadius,
                                      double bevel, int segments, ShapeLod lod = default)
        {
            Profile2 p = CapProfile.SetRoundedRect(sx, sz, cornerRadius, lod.Bevel(Math.Max(1, segments)) + (lod.Level == 0 ? 1 : 0));
            return BevelExtrude(m, xf, b, p.X, p.Y, p.Count, height, bevel, segments, BevelStyle.Round, true, true, 0, 30, lod);
        }

        // ------------------------------------------------------------------ sweep / tube

        /// <summary>
        /// Sweep a 2D <paramref name="section"/> along a 3D <paramref name="path"/>. The section's x maps to the
        /// frame's right (seen travelling along the path) and y to its up; <paramref name="frames"/> picks
        /// rotation-minimising or upright frames. Sharp path corners are mitred (the section is stretched in the
        /// bend so the width holds). The section scales linearly from <paramref name="scaleStart"/> to
        /// <paramref name="scaleEnd"/> (tapers: branches, horns, tails) and twists by <paramref name="twistDeg"/>.
        /// A closed section on an open path gets end caps when <paramref name="caps"/>. Railings, pipes, frames,
        /// handlebars, wires, kerbs, mouldings.
        /// </summary>
        public static int Sweep(MeshData m, in Affine3 xf, in ShapeBrush b, Path3 path, Profile2 section, bool closedPath = false,
                                bool caps = true, SweepFrames frames = SweepFrames.ParallelTransport, double scaleStart = 1,
                                double scaleEnd = 1, double twistDeg = 0)
        {
            int first = m.VertexCount;
            // Distinct path points.
            int np = 0;
            double[] PX = ShapeEmit.Scratch(0, path.Count + 1), PY = ShapeEmit.Scratch(1, path.Count + 1), PZ = ShapeEmit.Scratch(2, path.Count + 1);
            for (int i = 0; i < path.Count; i++)
            {
                if (np > 0)
                {
                    double ddx = path.X[i] - PX[np - 1], ddy = path.Y[i] - PY[np - 1], ddz = path.Z[i] - PZ[np - 1];
                    if (ddx * ddx + ddy * ddy + ddz * ddz < 1e-14) continue;
                }
                PX[np] = path.X[i];
                PY[np] = path.Y[i];
                PZ[np] = path.Z[i];
                np++;
            }
            if (closedPath && np > 2)
            {
                double ddx = PX[0] - PX[np - 1], ddy = PY[0] - PY[np - 1], ddz = PZ[0] - PZ[np - 1];
                if (ddx * ddx + ddy * ddy + ddz * ddz < 1e-14) np--;
            }
            if (np < 2) return first;
            if (np < 3) closedPath = false;
            Expanded e = ShapeEmit.ExpA;
            int s = ShapeEmit.Expand(section, e);
            if (s < 2) return first;
            // Frames: T (tangent), U (up), miter direction and factor, cumulative length.
            double[] TX = ShapeEmit.Scratch(3, np), TY = ShapeEmit.Scratch(4, np), TZ = ShapeEmit.Scratch(5, np);
            double[] UX = ShapeEmit.Scratch(6, np), UY = ShapeEmit.Scratch(7, np), UZ = ShapeEmit.Scratch(8, np);
            double total = 0;
            for (int i = 0; i < np; i++)
            {
                double ix, iy, iz, ox, oy, oz;
                bool hasIn = SegDir(PX, PY, PZ, np, i, -1, closedPath, out ix, out iy, out iz);
                bool hasOut = SegDir(PX, PY, PZ, np, i, +1, closedPath, out ox, out oy, out oz);
                double tx = (hasIn ? ix : 0) + (hasOut ? ox : 0), ty = (hasIn ? iy : 0) + (hasOut ? oy : 0), tz = (hasIn ? iz : 0) + (hasOut ? oz : 0);
                double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
                if (tl < 1e-9)
                {
                    tx = ox;
                    ty = oy;
                    tz = oz;
                    tl = 1;
                }
                TX[i] = tx / tl;
                TY[i] = ty / tl;
                TZ[i] = tz / tl;
                if (i > 0)
                {
                    double ddx = PX[i] - PX[i - 1], ddy = PY[i] - PY[i - 1], ddz = PZ[i] - PZ[i - 1];
                    total += Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                }
            }
            if (frames == SweepFrames.Upright)
            {
                double lx = 0, ly = 0, lz = 1;
                for (int i = 0; i < np; i++)
                {
                    double ux = -TX[i] * TY[i], uy = 1 - TY[i] * TY[i], uz = -TZ[i] * TY[i];
                    double ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                    if (ul < 1e-6)
                    {
                        // Vertical tangent: keep the previous up projected.
                        double d = lx * TX[i] + ly * TY[i] + lz * TZ[i];
                        ux = lx - d * TX[i];
                        uy = ly - d * TY[i];
                        uz = lz - d * TZ[i];
                        ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                        if (ul < 1e-9)
                        {
                            ux = 1;
                            uy = 0;
                            uz = 0;
                            ul = 1;
                        }
                    }
                    UX[i] = lx = ux / ul;
                    UY[i] = ly = uy / ul;
                    UZ[i] = lz = uz / ul;
                }
            }
            else
            {
                // Start upright, then double-reflection parallel transport (Wang et al. 2008).
                double ux = -TX[0] * TY[0], uy = 1 - TY[0] * TY[0], uz = -TZ[0] * TY[0];
                double ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                if (ul < 1e-6)
                {
                    ux = 1 - TX[0] * TX[0];
                    uy = -TY[0] * TX[0];
                    uz = -TZ[0] * TX[0];
                    ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                }
                UX[0] = ux / ul;
                UY[0] = uy / ul;
                UZ[0] = uz / ul;
                for (int i = 1; i < np; i++) Transport(PX, PY, PZ, TX, TY, TZ, UX, UY, UZ, i - 1, i);
                if (closedPath)
                {
                    // Close the loop: transport the last frame back to the start and spread the angle mismatch.
                    double sux = UX[np - 1], suy = UY[np - 1], suz = UZ[np - 1];
                    double rx, ry, rz;
                    TransportOne(PX[np - 1], PY[np - 1], PZ[np - 1], TX[np - 1], TY[np - 1], TZ[np - 1], sux, suy, suz, PX[0], PY[0], PZ[0], TX[0], TY[0], TZ[0],
                                 out rx, out ry, out rz);
                    // Signed angle from the transported up to the start up, about T0.
                    double cx = ry * UZ[0] - rz * UY[0], cy = rz * UX[0] - rx * UZ[0], cz = rx * UY[0] - ry * UX[0];
                    double sin = cx * TX[0] + cy * TY[0] + cz * TZ[0], cos = rx * UX[0] + ry * UY[0] + rz * UZ[0];
                    double ang = Math.Atan2(sin, cos);
                    double run = 0, loop = total + Math.Sqrt((PX[0] - PX[np - 1]) * (PX[0] - PX[np - 1]) + (PY[0] - PY[np - 1]) * (PY[0] - PY[np - 1]) +
                                                            (PZ[0] - PZ[np - 1]) * (PZ[0] - PZ[np - 1]));
                    for (int i = 1; i < np; i++)
                    {
                        double ddx = PX[i] - PX[i - 1], ddy = PY[i] - PY[i - 1], ddz = PZ[i] - PZ[i - 1];
                        run += Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                        RotateAbout(ref UX[i], ref UY[i], ref UZ[i], TX[i], TY[i], TZ[i], ang * run / loop);
                    }
                }
            }
            // Emit the rings.
            m.Reserve(np * s, np * s * 6);
            double runLen = 0;
            for (int i = 0; i < np; i++)
            {
                if (i > 0)
                {
                    double ddx = PX[i] - PX[i - 1], ddy = PY[i] - PY[i - 1], ddz = PZ[i] - PZ[i - 1];
                    runLen += Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                }
                double f = total > 1e-12 ? runLen / total : 0;
                double sc = scaleStart + (scaleEnd - scaleStart) * f, tw = twistDeg * f * Math.PI / 180.0;
                double ct = Math.Cos(tw), st = Math.Sin(tw);
                double tx = TX[i], ty = TY[i], tz = TZ[i], ux = UX[i], uy = UY[i], uz = UZ[i];
                // Right = U × T.
                double rx = uy * tz - uz * ty, ry = uz * tx - ux * tz, rz = ux * ty - uy * tx;
                // Miter: bend direction and stretch 1/cos(half turn).
                double bx = 0, by = 0, bz = 0, stretch = 1;
                double ix, iy, iz, ox, oy, oz;
                if (SegDir(PX, PY, PZ, np, i, -1, closedPath, out ix, out iy, out iz) && SegDir(PX, PY, PZ, np, i, +1, closedPath, out ox, out oy, out oz))
                {
                    double hx = ix + ox, hy = iy + oy, hz = iz + oz;
                    double ch = 0.5 * Math.Sqrt(hx * hx + hy * hy + hz * hz);
                    if (ch < 0.999 && ch > 1e-3)
                    {
                        bx = ox - ix;
                        by = oy - iy;
                        bz = oz - iz;
                        double bl = Math.Sqrt(bx * bx + by * by + bz * bz);
                        bx /= bl;
                        by /= bl;
                        bz /= bl;
                        stretch = Math.Min(1 / ch, 3);
                    }
                }
                for (int k = 0; k < s; k++)
                {
                    double lx = (e.X[k] * ct - e.Y[k] * st) * sc, ly = (e.X[k] * st + e.Y[k] * ct) * sc;
                    double lnx = e.NX[k] * ct - e.NY[k] * st, lny = e.NX[k] * st + e.NY[k] * ct;
                    double ofx = rx * lx + ux * ly, ofy = ry * lx + uy * ly, ofz = rz * lx + uz * ly;
                    double nx = rx * lnx + ux * lny, ny = ry * lnx + uy * lny, nz = rz * lnx + uz * lny;
                    if (stretch > 1)
                    {
                        double d = (bx * ofx + by * ofy + bz * ofz) * (stretch - 1);
                        ofx += bx * d;
                        ofy += by * d;
                        ofz += bz * d;
                        double dn = (bx * nx + by * ny + bz * nz) * (1 - 1 / stretch);
                        nx -= bx * dn;
                        ny -= by * dn;
                        nz -= bz * dn;
                    }
                    ShapeEmit.Normalize(ref nx, ref ny, ref nz);
                    ShapeEmit.Vertex(m, xf, b, PX[i] + ofx, PY[i] + ofy, PZ[i] + ofz, nx, ny, nz);
                }
            }
            ShapeEmit.Grid(m, first, np, s, section.Closed, closedPath, e.Seam, null);
            if (caps && section.Closed && !closedPath)
            {
                SweepCap(m, xf, b, section, PX[0], PY[0], PZ[0], TX[0], TY[0], TZ[0], UX[0], UY[0], UZ[0], scaleStart, 0, -1);
                SweepCap(m, xf, b, section, PX[np - 1], PY[np - 1], PZ[np - 1], TX[np - 1], TY[np - 1], TZ[np - 1], UX[np - 1], UY[np - 1],
                         UZ[np - 1], scaleEnd, twistDeg * Math.PI / 180.0, 1);
            }
            return first;
        }

        private static void SweepCap(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 section, double px, double py, double pz,
                                     double tx, double ty, double tz, double ux, double uy, double uz, double sc, double tw, double dir)
        {
            int[] tris, idx;
            int pc;
            int tc = ShapeEmit.TriangulateProfile(section, out tris, out idx, out pc);
            if (tc == 0) return;
            double rx = uy * tz - uz * ty, ry = uz * tx - ux * tz, rz = ux * ty - uy * tx;
            double ct = Math.Cos(tw), st = Math.Sin(tw);
            int v0 = m.VertexCount;
            m.Reserve(pc, tc * 3);
            for (int k = 0; k < pc; k++)
            {
                double x0 = section.X[idx[k]], y0 = section.Y[idx[k]];
                double lx = (x0 * ct - y0 * st) * sc, ly = (x0 * st + y0 * ct) * sc;
                ShapeEmit.Vertex(m, xf, b, px + rx * lx + ux * ly, py + ry * lx + uy * ly, pz + rz * lx + uz * ly, tx * dir, ty * dir, tz * dir);
            }
            for (int t = 0; t < tc; t++) ShapeEmit.TriOriented(m, v0 + tris[3 * t], v0 + tris[3 * t + 1], v0 + tris[3 * t + 2]);
        }

        /// <summary>Unit direction of the segment before (dir -1) or after (dir +1) point i; false at an open end.</summary>
        private static bool SegDir(double[] x, double[] y, double[] z, int n, int i, int dir, bool closed, out double dx, out double dy, out double dz)
        {
            int j = i + dir;
            if (closed) j = (j + n) % n;
            dx = dy = dz = 0;
            if (j < 0 || j >= n) return false;
            int a = dir < 0 ? j : i, c = dir < 0 ? i : j;
            dx = x[c] - x[a];
            dy = y[c] - y[a];
            dz = z[c] - z[a];
            double l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (l < 1e-12) return false;
            dx /= l;
            dy /= l;
            dz /= l;
            return true;
        }

        private static void Transport(double[] PX, double[] PY, double[] PZ, double[] TX, double[] TY, double[] TZ,
                                      double[] UX, double[] UY, double[] UZ, int a, int c)
        {
            double rx, ry, rz;
            TransportOne(PX[a], PY[a], PZ[a], TX[a], TY[a], TZ[a], UX[a], UY[a], UZ[a], PX[c], PY[c], PZ[c], TX[c], TY[c], TZ[c],
                         out rx, out ry, out rz);
            UX[c] = rx;
            UY[c] = ry;
            UZ[c] = rz;
        }

        private static void TransportOne(double p0x, double p0y, double p0z, double t0x, double t0y, double t0z, double r0x, double r0y, double r0z,
                                         double p1x, double p1y, double p1z, double t1x, double t1y, double t1z,
                                         out double rx, out double ry, out double rz)
        {
            double v1x = p1x - p0x, v1y = p1y - p0y, v1z = p1z - p0z;
            double c1 = v1x * v1x + v1y * v1y + v1z * v1z;
            double rlx = r0x, rly = r0y, rlz = r0z, tlx = t0x, tly = t0y, tlz = t0z;
            if (c1 > 1e-18)
            {
                double k = 2 / c1 * (v1x * r0x + v1y * r0y + v1z * r0z);
                rlx = r0x - k * v1x;
                rly = r0y - k * v1y;
                rlz = r0z - k * v1z;
                k = 2 / c1 * (v1x * t0x + v1y * t0y + v1z * t0z);
                tlx = t0x - k * v1x;
                tly = t0y - k * v1y;
                tlz = t0z - k * v1z;
            }
            double v2x = t1x - tlx, v2y = t1y - tly, v2z = t1z - tlz;
            double c2 = v2x * v2x + v2y * v2y + v2z * v2z;
            rx = rlx;
            ry = rly;
            rz = rlz;
            if (c2 > 1e-18)
            {
                double k = 2 / c2 * (v2x * rlx + v2y * rly + v2z * rlz);
                rx = rlx - k * v2x;
                ry = rly - k * v2y;
                rz = rlz - k * v2z;
            }
            // Re-orthonormalise against t1.
            double d = rx * t1x + ry * t1y + rz * t1z;
            rx -= d * t1x;
            ry -= d * t1y;
            rz -= d * t1z;
            double l = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            if (l < 1e-12)
            {
                rx = r0x;
                ry = r0y;
                rz = r0z;
                return;
            }
            rx /= l;
            ry /= l;
            rz /= l;
        }

        private static void RotateAbout(ref double x, ref double y, ref double z, double ax, double ay, double az, double ang)
        {
            double c = Math.Cos(ang), s = Math.Sin(ang), d = ax * x + ay * y + az * z;
            double cx = ay * z - az * y, cy = az * x - ax * z, cz = ax * y - ay * x;
            double nx = x * c + cx * s + ax * d * (1 - c);
            double ny = y * c + cy * s + ay * d * (1 - c);
            double nz = z * c + cz * s + az * d * (1 - c);
            x = nx;
            y = ny;
            z = nz;
        }

        /// <summary>A round tube of <paramref name="radius"/> along a path (tapering to <paramref name="radiusEnd"/>
        /// when ≥ 0), capped at open ends when <paramref name="caps"/>. Pipes, rails, handlebars, wires, branches.</summary>
        public static int Tube(MeshData m, in Affine3 xf, in ShapeBrush b, Path3 path, double radius, int radialSegments, bool caps = true,
                               bool closedPath = false, ShapeLod lod = default, double radiusEnd = -1)
        {
            if (s_circle == null) s_circle = new Profile2(64);
            s_circle.SetCircle(radius, lod.Radial(radialSegments));
            double se = radiusEnd >= 0 && radius > 0 ? radiusEnd / radius : 1;
            return Sweep(m, xf, b, path, s_circle, closedPath, caps, SweepFrames.ParallelTransport, 1, se, 0);
        }

        /// <summary>A round bar (capped tube) between two points.</summary>
        public static int Bar(MeshData m, in Affine3 xf, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz,
                              double radius, int radialSegments, ShapeLod lod = default)
        {
            if (s_path == null) s_path = new Path3(8);
            s_path.Clear().Add(ax, ay, az).Add(bx, by, bz);
            return Tube(m, xf, b, s_path, radius, radialSegments, true, false, lod);
        }

        /// <summary>A hanging wire (catenary, see <see cref="Curves.Catenary"/>) of <paramref name="radius"/> from a
        /// to b sagging <paramref name="sag"/> metres at mid-span.</summary>
        public static int Wire(MeshData m, in Affine3 xf, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz,
                               double sag, double radius, int segments, int radialSegments = 4, ShapeLod lod = default)
        {
            if (s_path == null) s_path = new Path3(32);
            Curves.Catenary(s_path.Clear(), ax, ay, az, bx, by, bz, sag, lod.Path(segments));
            return Tube(m, xf, b, s_path, radius, radialSegments, false, false, lod);
        }

        // ------------------------------------------------------------------ loft

        /// <summary>
        /// Loft between two closed profiles, each placed in the local XY plane of its own frame (profile (x, y) maps
        /// to frame (x, y, 0)), with <paramref name="segments"/> straight rings between them. Profiles of different
        /// point counts are resampled by arc length from their first points. Optional caps. Vase necks, tree trunks
        /// that change cross-section, car noses, roof finials, transitions between a square base and a round shaft.
        /// </summary>
        public static int Loft(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 a, in Affine3 frameA, Profile2 bProf, in Affine3 frameB,
                               int segments, bool capA = true, bool capB = true, ShapeLod lod = default)
        {
            int first = m.VertexCount;
            int n = Math.Max(a.Count, bProf.Count);
            if (n < 3) return first;
            int rings = lod.Path(Math.Max(1, segments)) + 1;
            double[] ax = ShapeEmit.Scratch(0, n), ay = ShapeEmit.Scratch(1, n), az = ShapeEmit.Scratch(2, n);
            double[] bx = ShapeEmit.Scratch(3, n), by = ShapeEmit.Scratch(4, n), bz = ShapeEmit.Scratch(5, n);
            double[] au = ShapeEmit.Scratch(9, n), av = ShapeEmit.Scratch(10, n), bu = ShapeEmit.Scratch(11, n), bv = ShapeEmit.Scratch(12, n);
            PlaceRing(a, frameA, n, au, av, ax, ay, az);
            PlaceRing(bProf, frameB, n, bu, bv, bx, by, bz);
            AlignRing(au, av, bu, bv, bx, by, bz, n);
            // Outward reference: ring centroids.
            double cax = 0, cay = 0, caz = 0, cbx = 0, cby = 0, cbz = 0;
            for (int k = 0; k < n; k++)
            {
                cax += ax[k];
                cay += ay[k];
                caz += az[k];
                cbx += bx[k];
                cby += by[k];
                cbz += bz[k];
            }
            cax /= n;
            cay /= n;
            caz /= n;
            cbx /= n;
            cby /= n;
            cbz /= n;
            // Orientation of cross(along ring, along loft) relative to outward, decided once.
            double vote = 0;
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n, k0 = (k + n - 1) % n;
                double ux = ax[k1] - ax[k0] + bx[k1] - bx[k0], uy = ay[k1] - ay[k0] + by[k1] - by[k0], uz = az[k1] - az[k0] + bz[k1] - bz[k0];
                double vx = bx[k] - ax[k], vy = by[k] - ay[k], vz = bz[k] - az[k];
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double ox = 0.5 * (ax[k] + bx[k] - cax - cbx), oy = 0.5 * (ay[k] + by[k] - cay - cby), oz = 0.5 * (az[k] + bz[k] - caz - cbz);
                vote += nx * ox + ny * oy + nz * oz > 0 ? 1 : -1;
            }
            double sign = vote >= 0 ? 1 : -1;
            m.Reserve(rings * n, rings * n * 6);
            for (int r = 0; r < rings; r++)
            {
                double t = rings > 1 ? (double)r / (rings - 1) : 0;
                for (int k = 0; k < n; k++)
                {
                    int k1 = (k + 1) % n, k0 = (k + n - 1) % n;
                    double ux = (1 - t) * (ax[k1] - ax[k0]) + t * (bx[k1] - bx[k0]);
                    double uy = (1 - t) * (ay[k1] - ay[k0]) + t * (by[k1] - by[k0]);
                    double uz = (1 - t) * (az[k1] - az[k0]) + t * (bz[k1] - bz[k0]);
                    double vx = bx[k] - ax[k], vy = by[k] - ay[k], vz = bz[k] - az[k];
                    double nx = (uy * vz - uz * vy) * sign, ny = (uz * vx - ux * vz) * sign, nz = (ux * vy - uy * vx) * sign;
                    ShapeEmit.Normalize(ref nx, ref ny, ref nz);
                    ShapeEmit.Vertex(m, xf, b, ax[k] + (bx[k] - ax[k]) * t, ay[k] + (by[k] - ay[k]) * t, az[k] + (bz[k] - az[k]) * t, nx, ny, nz);
                }
            }
            ShapeEmit.Grid(m, first, rings, n, true, false, null, null);
            if (capA) LoftCap(m, xf, b, au, av, n, frameA, cax - cbx, cay - cby, caz - cbz);
            if (capB) LoftCap(m, xf, b, bu, bv, n, frameB, cbx - cax, cby - cay, cbz - caz);
            return first;
        }

        /// <summary>Profile p resampled to n points (2D in u, v; placed by the frame in x, y, z).</summary>
        private static void PlaceRing(Profile2 p, in Affine3 frame, int n, double[] u, double[] v, double[] x, double[] y, double[] z)
        {
            if (p.Count == n)
            {
                for (int k = 0; k < n; k++)
                {
                    u[k] = p.X[k];
                    v[k] = p.Y[k];
                }
            }
            else
            {
                double total = p.Length(), step = total / n, segStart = 0;
                int seg = 0;
                for (int k = 0; k < n; k++)
                {
                    double target = k * step;
                    while (seg < p.Count - 1)
                    {
                        int j = seg + 1;
                        double l = Math.Sqrt((p.X[j] - p.X[seg]) * (p.X[j] - p.X[seg]) + (p.Y[j] - p.Y[seg]) * (p.Y[j] - p.Y[seg]));
                        if (segStart + l >= target) break;
                        segStart += l;
                        seg++;
                    }
                    int jn = (seg + 1) % p.Count;
                    double sl = Math.Sqrt((p.X[jn] - p.X[seg]) * (p.X[jn] - p.X[seg]) + (p.Y[jn] - p.Y[seg]) * (p.Y[jn] - p.Y[seg]));
                    double f = sl < 1e-12 ? 0 : Math.Min(1, (target - segStart) / sl);
                    u[k] = p.X[seg] + (p.X[jn] - p.X[seg]) * f;
                    v[k] = p.Y[seg] + (p.Y[jn] - p.Y[seg]) * f;
                }
            }
            for (int k = 0; k < n; k++) frame.Point(u[k], v[k], 0, out x[k], out y[k], out z[k]);
        }

        /// <summary>Rotate ring B's start so its points pair with ring A's nearest ones (in their local 2D
        /// coordinates), which keeps a loft from twisting when the two profiles start at different corners.</summary>
        private static void AlignRing(double[] au, double[] av, double[] bu, double[] bv, double[] bx, double[] by, double[] bz, int n)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int s = 0; s < n; s++)
            {
                double d = 0;
                for (int k = 0; k < n && d < bestD; k++)
                {
                    int j = (k + s) % n;
                    double du = au[k] - bu[j], dv = av[k] - bv[j];
                    d += du * du + dv * dv;
                }
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            if (best == 0) return;
            Rotate(bu, n, best, ShapeEmit.Scratch(13, n));
            Rotate(bv, n, best, ShapeEmit.Scratch(13, n));
            Rotate(bx, n, best, ShapeEmit.Scratch(13, n));
            Rotate(by, n, best, ShapeEmit.Scratch(13, n));
            Rotate(bz, n, best, ShapeEmit.Scratch(13, n));
        }

        private static void Rotate(double[] a, int n, int shift, double[] tmp)
        {
            for (int k = 0; k < n; k++) tmp[k] = a[(k + shift) % n];
            Array.Copy(tmp, a, n);
        }

        private static void LoftCap(MeshData m, in Affine3 xf, in ShapeBrush b, double[] u, double[] v, int n, in Affine3 frame,
                                    double dx, double dy, double dz)
        {
            int[] tris, idx;
            int pc;
            int tc = ShapeEmit.TriangulateArrays(u, v, n, out tris, out idx, out pc);
            if (tc == 0) return;
            // Cap normal: the frame's ±Z, on the side facing away from the other profile.
            double nx, ny, nz;
            frame.Normal(0, 0, 1, out nx, out ny, out nz);
            if (nx * dx + ny * dy + nz * dz < 0)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            int v0 = m.VertexCount;
            m.Reserve(pc, tc * 3);
            for (int k = 0; k < pc; k++)
            {
                double x, y, z;
                frame.Point(u[idx[k]], v[idx[k]], 0, out x, out y, out z);
                ShapeEmit.Vertex(m, xf, b, x, y, z, nx, ny, nz);
            }
            for (int t = 0; t < tc; t++) ShapeEmit.TriOriented(m, v0 + tris[3 * t], v0 + tris[3 * t + 1], v0 + tris[3 * t + 2]);
        }

        // ------------------------------------------------------------------ instancing

        /// <summary>
        /// Append a copy of <paramref name="src"/>'s vertices [firstVertex, firstVertex + vertexCount) and the
        /// triangles [firstIndex, firstIndex + indexCount) that use them, transformed by <paramref name="xf"/> and
        /// with RGB multiplied by <paramref name="tint"/> (0xFFFFFFFF = unchanged; alpha kept). Pass -1 counts for
        /// "to the end". <paramref name="src"/> may be <paramref name="m"/> itself (instance a part you just built:
        /// the spokes of a wheel, the posts of a railing). Mirroring transforms flip the winding. Returns the first
        /// new vertex.
        /// </summary>
        public static int CopyTransformed(MeshData m, MeshData src, in Affine3 xf, uint tint = 0xFFFFFFFFu, int firstVertex = 0,
                                          int vertexCount = -1, int firstIndex = 0, int indexCount = -1)
        {
            if (vertexCount < 0) vertexCount = src.VertexCount - firstVertex;
            if (indexCount < 0) indexCount = src.IndexCount - firstIndex;
            int v0 = m.VertexCount;
            m.Reserve(vertexCount, indexCount);
            bool uv = src.HasUv0;
            bool flip = xf.Mirrors;
            bool tinted = tint != 0xFFFFFFFFu;
            int tr = MeshColor.R(tint), tg = MeshColor.G(tint), tb = MeshColor.B(tint);
            for (int i = 0; i < vertexCount; i++)
            {
                int v = firstVertex + i;
                float[] p = src.Positions, nn = src.Normals;
                double x, y, z, nx, ny, nz;
                xf.Point(p[3 * v], p[3 * v + 1], p[3 * v + 2], out x, out y, out z);
                xf.Normal(nn[3 * v], nn[3 * v + 1], nn[3 * v + 2], out nx, out ny, out nz);
                byte[] cs = src.Colors;
                uint c = (uint)cs[4 * v] << 24 | (uint)cs[4 * v + 1] << 16 | (uint)cs[4 * v + 2] << 8 | cs[4 * v + 3];
                if (tinted) c = MeshColor.Pack(MeshColor.R(c) * tr / 255, MeshColor.G(c) * tg / 255, MeshColor.B(c) * tb / 255, MeshColor.A(c));
                if (uv) m.AddVertex((float)x, (float)y, (float)z, (float)nx, (float)ny, (float)nz, c, src.Uv0[2 * v], src.Uv0[2 * v + 1]);
                else m.AddVertex((float)x, (float)y, (float)z, (float)nx, (float)ny, (float)nz, c);
            }
            int delta = v0 - firstVertex;
            for (int i = 0; i + 2 < indexCount; i += 3)
            {
                int[] ix = src.Indices;
                int a = ix[firstIndex + i], bb = ix[firstIndex + i + 1], cc = ix[firstIndex + i + 2];
                if (a < firstVertex || a >= firstVertex + vertexCount || bb < firstVertex || bb >= firstVertex + vertexCount ||
                    cc < firstVertex || cc >= firstVertex + vertexCount) continue;
                if (flip) m.AddTriangle(a + delta, cc + delta, bb + delta);
                else m.AddTriangle(a + delta, bb + delta, cc + delta);
            }
            return v0;
        }
    }
}
