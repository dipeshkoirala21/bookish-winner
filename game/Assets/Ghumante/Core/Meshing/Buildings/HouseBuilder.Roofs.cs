using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;

namespace Ghumante.Core.Meshing
{
    internal static partial class HouseBuilder
    {
        private static void Roof(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            if (p.Gable) NewarRoof(ref h, s, ref p, ref rng, m);
            else FlatRoof(ref h, s, ref p, ref rng, m);
        }

        // =============================================================================================================
        // The Newar jhingati gable
        // =============================================================================================================

        /// <summary>
        /// The jhingati gable over a plot with its ridge parallel to the street: a 0.18 m slab whose top steps down in
        /// tile courses, a fascia board, the soffit under the overhang (1.0 m, 1.2 m in Bhaktapur, clipped out of the
        /// road below 4.5 m), a rounded ridge cap with raised ends, brick gable triangles and barge boards at the row
        /// ends, and carved struts (tundal) on a wall plate under the street eave (a fascia stripe from drop level 2).
        /// </summary>
        private static void NewarRoof(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            KitFrame f = h.F;
            double D = p.Depth, top = p.Top, half = 0.5 * D, rise = p.RidgeV - top, tan = rise / half;
            const double Slab = 0.18;
            bool first = p.Index == 0, lastPlot = p.Index == h.Plots - 1;
            double a = p.U0 - (first ? 0.35 : 0), b = p.U1 + (lastPlot ? 0.35 : 0);
            double ovr = h.Style.EaveOverhangM;
            double dF = Allow(ref h, f, a, b, top - ovr * tan - Slab, ovr);
            // The back frame: origin at the back-right corner, facing away from the street.
            double bx, by, bz;
            f.ToWorld(b, 0, -D, out bx, out by, out bz);
            var fb = new KitFrame(bx, f.OY, bz, -f.UX, -f.UZ);
            double dB = Allow(ref h, fb, 0, b - a, top - ovr * tan - Slab, ovr);
            uint tile = p.Roof, under = MeshColor.Scale(BuildingGrammar.SalDark, 1.15f);
            int vs = m.VertexCount;
            Slope(ref h, f, a, b, dF, half, top, tan, Slab, tile, under, m);
            Slope(ref h, fb, 0, b - a, dB, half, top, tan, Slab, tile, under, m);
            // Tiles vs wood: the sweep's first two segments (soffit, fascia) are wood; repaint them by normal.
            PaintRoof(ref h, m, vs, top);

            // Ridge cap with raised ends.
            vs = m.VertexCount;
            double r0x, r0y, r0z, r1x, r1y, r1z;
            f.ToWorld(a - 0.05, p.RidgeV + 0.05, -half, out r0x, out r0y, out r0z);
            f.ToWorld(b + 0.05, p.RidgeV + 0.05, -half, out r1x, out r1y, out r1z);
            KitRound.Rod(m, r0x, r0y, r0z, r1x, r1y, r1z, 0.1, h.Det.Segs > 0 ? 6 : 4, true, BuildingGrammar.JhingatiRidge);
            if (h.Det.Small && (first || lastPlot))
            {
                if (first) KitRound.Ellipsoid(m, r0x, r0y + 0.1, r0z, 0.13, 0.17, 0.13, 6, 3, BuildingGrammar.JhingatiRidge);
                if (lastPlot) KitRound.Ellipsoid(m, r1x, r1y + 0.1, r1z, 0.13, 0.17, 0.13, 6, 3, BuildingGrammar.JhingatiRidge);
            }
            KitPaint.Of(MaterialChannel.RoofTile, 0.95f).Apply(m, vs);

            // Gable triangles (wall) at the plot ends and barge boards on the overhangs at the row ends.
            vs = m.VertexCount;
            for (int e = 0; e < 2; e++)
            {
                double u = e == 0 ? p.U0 : p.U1;
                bool exposed = e == 0 ? first || s.Plots[p.Index - 1].RidgeV < p.RidgeV - 0.05 || !s.Plots[p.Index - 1].Gable
                                      : lastPlot || s.Plots[p.Index + 1].RidgeV < p.RidgeV - 0.05 || !s.Plots[p.Index + 1].Gable;
                if (!exposed) continue;
                MeshKit.TriLocal(m, f, u, top, 0, u, top, -D, u, p.RidgeV, -half, e == 0 ? -1 : 1, 0, 0, p.Wall);
            }
            WallPaint(ref h, p.WallCh, 1f).WithTop(h.Ground + p.RidgeV, 0.7f, 1.2f).Apply(m, vs);
            vs = m.VertexCount;
            for (int e = 0; e < 2; e++)
            {
                if (e == 0 ? !first : !lastPlot) continue;
                double u = e == 0 ? a : b, side = e == 0 ? -1 : 1;
                // A board along each slope at the end of the overhang.
                for (int sl = 0; sl < 2; sl++)
                {
                    double wE = sl == 0 ? dF : -D - dB, vE = top - (sl == 0 ? dF : dB) * tan;
                    double x0, y0, z0, x1, y1, z1;
                    f.ToWorld(u, vE - 0.04, wE, out x0, out y0, out z0);
                    f.ToWorld(u, p.RidgeV - 0.02, -half, out x1, out y1, out z1);
                    double dx = x1 - x0, dy = y1 - y0, dz = z1 - z0, l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (l < 0.2) continue;
                    double ex, ey, ez, fx2, fy2, fz2;
                    f.ToWorld(u, vE - 0.2, wE, out ex, out ey, out ez);
                    f.ToWorld(u, p.RidgeV - 0.22, -half, out fx2, out fy2, out fz2);
                    MeshKit.Quad(m, x0, y0 + 0.06, z0, x1, y1 + 0.06, z1, fx2, fy2, fz2, ex, ey, ez, side * f.UX, 0, side * f.UZ, MeshColor.Scale(p.Wood, 1.1f));
                }
                // Soffit of the gable overhang.
                double uIn = e == 0 ? p.U0 : p.U1;
                MeshKit.QuadLocal(m, f, Math.Min(u, uIn), top - dF * tan - Slab, dF, Math.Max(u, uIn), top - dF * tan - Slab, dF,
                                  Math.Max(u, uIn), p.RidgeV - Slab, -half, Math.Min(u, uIn), p.RidgeV - Slab, -half, 0, -1, 0.2, under);
                MeshKit.QuadLocal(m, f, Math.Min(u, uIn), top - dB * tan - Slab, -D - dB, Math.Max(u, uIn), top - dB * tan - Slab, -D - dB,
                                  Math.Max(u, uIn), p.RidgeV - Slab, -half, Math.Min(u, uIn), p.RidgeV - Slab, -half, 0, -1, -0.2, under);
            }
            KitPaint.Of(MaterialChannel.Wood, 0.8f).Apply(m, vs);

            // Struts on a wall plate under the street eave (and a fascia stripe when they are dropped).
            if (dF > 0.3)
            {
                vs = m.VertexCount;
                double wTop = dF - 0.2, vTop = top - wTop * tan - Slab - 0.02;
                // The strut feet sit on a wall plate low enough for the struts to rise at about 50 degrees.
                double plate = Math.Max(vTop - 1.2 * wTop, FloorBase(s, p, Math.Max(0, p.Storeys - 1)) + 0.2);
                if (h.Det.Struts)
                {
                    FacadeKit.Ledge(m, f, p.FU0, p.FU1, plate - 0.16, 0.2, 0.1, 0.04, 3, MeshColor.Scale(p.Wood, 1.05f));
                    double spacing = rng.Range(1.15f, 1.45f);
                    int count = Math.Max(2, (int)Math.Round((p.FU1 - p.FU0) / spacing));
                    for (int k = 0; k < count; k++)
                    {
                        double u = p.FU0 + (k + 0.5) * (p.FU1 - p.FU0) / count;
                        Strut(m, f, u, plate + 0.04, 0.06, vTop, wTop, 0.17, MeshColor.Scale(p.Wood, 0.9f), h.Det.Segs);
                    }
                    if (h.Det.Small)
                    {
                        // Rafters under the soffit.
                        for (double u = a + 0.25; u < b - 0.1; u += 0.5)
                        {
                            double x0, y0, z0, x1, y1, z1;
                            f.ToWorld(u, top - Slab - 0.04, 0.02, out x0, out y0, out z0);
                            f.ToWorld(u, top - dF * tan - Slab - 0.04, dF - 0.05, out x1, out y1, out z1);
                            MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.07, MeshColor.Scale(BuildingGrammar.SalDark, 0.9f));
                        }
                    }
                }
                else MeshKit.Panel(m, f, p.FU0, top - 0.35, p.FU1, top - 0.02, 0.01, p.Wood);
                KitPaint.Of(MaterialChannel.WoodCarved, 0.85f).WithTop(h.Ground + top, 0.65f, 0.8f).Apply(m, vs);
            }
        }

        /// <summary>One roof slope swept along U: soffit from the wall to the eave, fascia, tile courses up to the
        /// ridge at w = −half.</summary>
        private static void Slope(ref House h, in KitFrame f, double u0, double u1, double d, double half, double top, double tan, double slab, uint tile,
                                  uint under, MeshData m)
        {
            double[] pw = FacadeKit.ProfileW, pv = FacadeKit.ProfileV;
            double vE = top - d * tan;
            int k = 0;
            if (d > 0.02)
            {
                pw[k] = 0;
                pv[k++] = top - slab;
                pw[k] = d;
                pv[k++] = vE - slab;
            }
            else
            {
                pw[k] = d;
                pv[k++] = vE - slab;
            }
            pw[k] = d;
            pv[k++] = vE;
            FacadeKit.SweepU(m, f, u0, u1, k, 0, under, 30);
            double len = Math.Sqrt((d + half) * (d + half) + (half * tan + d * tan) * (half * tan + d * tan));
            int rows = h.Det.Courses ? Math.Max(2, Math.Min(15, (int)(len / 0.34))) : 1;
            k = 0;
            pw[k] = d;
            pv[k++] = vE;
            for (int r = 1; r <= rows; r++)
            {
                double t = (double)r / rows, w = d + (-half - d) * t, v = vE + (top + half * tan - vE) * t;
                if (r < rows)
                {
                    // Course step: the lower lip of the next row stands 3 cm proud.
                    pw[k] = w + 0.012;
                    pv[k++] = v - 0.03;
                }
                pw[k] = w;
                pv[k++] = v;
            }
            FacadeKit.SweepU(m, f, u0, u1, k, 0, tile, 30);
        }

        /// <summary>Roof paint: faces pointing down or sideways at the eave are timber (soffit, fascia), the rest tile;
        /// soffits dark near the wall.</summary>
        private static void PaintRoof(ref House h, MeshData m, int v0, double top)
        {
            KitPaint.Begin(m);
            float[] n = m.Normals, pos = m.Positions, uv = m.Uv0;
            double wallY = h.Ground + top;
            for (int v = v0; v < m.VertexCount; v++)
            {
                double ny = n[3 * v + 1], y = pos[3 * v + 1];
                bool wood = ny < 0.2;
                uv[2 * v] = (float)(wood ? MaterialChannel.Wood : MaterialChannel.RoofTile);
                double ao = wood ? (ny < -0.3 ? 0.55 + 0.35 * Math.Min(1, Math.Max(0, (wallY - y) / 0.9)) : 0.85) : 1.0;
                uv[2 * v + 1] = (float)ao;
            }
        }

        // =============================================================================================================
        // Flat terraces
        // =============================================================================================================

        private static void FlatRoof(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            int at = p.DispStart, n = p.DispCount;
            double y = h.Ground + p.Top;
            // Deck.
            int vs = m.VertexCount;
            CopyDisplay(s, at, n);
            bool convex = Polygon.IsConvex(_subX, _subZ, n, 0.0);
            int tris = Polygon.Triangulate(_subX, _subZ, n, s.Tris, s.Next, s.Prev, convex);
            for (int t = 0; t < tris; t++)
            {
                int ia = s.Tris[3 * t], ib = s.Tris[3 * t + 1], ic = s.Tris[3 * t + 2];
                MeshKit.Tri(m, _subX[ia], y, _subZ[ia], _subX[ib], y, _subZ[ib], _subX[ic], y, _subZ[ic], 0, 1, 0, p.Roof);
            }
            KitPaint.Of(MaterialChannel.Concrete, 1f).Apply(m, vs);
            if (h.Colliders != null)
            {
                double ccx, ccy, ccz;
                h.F.ToWorld(0.5 * (p.U0 + p.U1), 0, -0.5 * p.Depth, out ccx, out ccy, out ccz);
                h.Colliders.AddBox(ccx, ccz, h.Base, y, 0.5 * (p.U1 - p.U0), 0.5 * p.Depth, h.F.UX, h.F.UZ, GenColliderFlags.Walkable, GenColliders.Concrete);
            }

            // Parapet: inner faces and a rounded coping along every edge (the front one on the cantilever line).
            uint inner = MeshColor.Scale(p.ExposedBrick || p.Arch == BuildingArchetype.NewarHybrid && p.ExposedBrick ? p.Wall : p.Front, 0.92f);
            uint coping = p.Arch == BuildingArchetype.RanaPalace ? BuildingGrammar.RanaTrim : p.ExposedBrick ? BuildingGrammar.Concrete : p.Trim;
            bool rail = p.Arch == BuildingArchetype.ModernUrban && h.Det.Rails && rng.Chance(0.22f);
            double par = p.Parapet;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                var kind = (Edge)s.DK[at + i];
                double ax = s.DX[at + i], az = s.DZ[at + i], bx = s.DX[at + j], bz = s.DZ[at + j];
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-3) continue;
                if (kind == Edge.PartitionLow || kind == Edge.PartitionHigh)
                {
                    Plot q = s.Plots[kind == Edge.PartitionLow ? p.Index - 1 : p.Index + 1];
                    double qTop = q.Gable ? q.RidgeV : q.Top + q.Parapet;
                    if (qTop > p.Top + par - 0.05) continue; // the neighbour's wall stands above this parapet
                }
                var f = new KitFrame(ax, h.Ground, az, dx, dz);
                double w0 = 0;
                if (kind == Edge.Front && p.Cant > 0)
                {
                    f = f.Offset(0, 0, p.Cant);
                    f = new KitFrame(f.OX, h.Ground, f.OZ, dx, dz);
                }
                int v0 = m.VertexCount;
                double pv0 = p.Top, pv1 = p.Top + par - 0.07;
                if (rail && kind == Edge.Front) pv1 = p.Top + 0.45;
                MeshKit.QuadLocal(m, f, 0, pv0, w0 - 0.11, len, pv0, w0 - 0.11, len, pv1, w0 - 0.11, 0, pv1, w0 - 0.11, 0, 0, -1, inner);
                WallPaint(ref h, p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.ExposedBrick ? MaterialChannel.Brick : p.FrontCh, 1f)
                    .WithGround(y, 0.55f, 0.5f).Apply(m, v0);
                v0 = m.VertexCount;
                if (h.Det.Bands || kind == Edge.Front)
                {
                    double ext = kind == Edge.Arc ? 0.0 : 0.06;
                    FacadeKit.Coping(m, f, -ext, len + ext, pv1, 0.07, 0.2, 0, coping);
                    // The top of the parapet wall under the coping is hidden; no extra faces.
                }
                Free(ref h, m, v0, MaterialChannel.Concrete);
                if (rail && kind == Edge.Front && len > 1.0)
                {
                    v0 = m.VertexCount;
                    int posts = Math.Max(2, (int)(len / 1.2) + 1);
                    uint rc = MeshColor.FromHex(0xD7DCE0);
                    for (int k = 0; k < posts; k++)
                    {
                        double u = 0.08 + (len - 0.16) * k / (posts - 1);
                        FacadeKit.RodLocal(m, f, u, pv1 + 0.07, -0.05, u, p.Top + 1.05, -0.05, 0.022, 4, false, rc);
                    }
                    FacadeKit.RodLocal(m, f, 0.05, p.Top + 1.05, -0.05, len - 0.05, p.Top + 1.05, -0.05, 0.026, 6, true, rc);
                    FacadeKit.RodLocal(m, f, 0.05, p.Top + 0.8, -0.05, len - 0.05, p.Top + 0.8, -0.05, 0.014, 5, false, rc);
                    Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                }
                if (p.Arch == BuildingArchetype.RanaPalace && kind == Edge.Front) Balustrade(ref h, f, 0, len, p.Top + 0.02, m);
            }
            if (h.Det.Props || p.Storeys >= 3) Props(ref h, s, ref p, ref rng, y, m);
        }

        [ThreadStatic] private static double[] _subX, _subZ;

        /// <summary>Copy a plot's display polygon into zero-based scratch arrays (the Polygon helpers take whole
        /// arrays).</summary>
        private static void CopyDisplay(Scratch s, int at, int n)
        {
            if (_subX == null || _subX.Length < n)
            {
                _subX = new double[Math.Max(64, 2 * n)];
                _subZ = new double[Math.Max(64, 2 * n)];
            }
            Array.Copy(s.DX, at, _subX, 0, n);
            Array.Copy(s.DZ, at, _subZ, 0, n);
        }

        /// <summary>A Rana balustrade along a parapet: plinth, bottle balusters, a top rail and urns on the piers.</summary>
        private static void Balustrade(ref House h, in KitFrame f, double u0, double u1, double v, MeshData m)
        {
            if (!h.Det.Rails) return;
            int v0 = m.VertexCount;
            uint c = BuildingGrammar.RanaTrim;
            double[] r = KitRound.ProfileR, y = KitRound.ProfileY;
            int count = Math.Max(3, (int)((u1 - u0) / 0.28));
            for (int i = 0; i < count; i++)
            {
                if (i % 6 == 0) continue; // a pier
                double u = u0 + (u1 - u0) * (i + 0.5) / count, x, yy, z;
                f.ToWorld(u, v + 0.12, 0.05, out x, out yy, out z);
                int k = 0;
                r[k] = 0.07;
                y[k++] = 0;
                r[k] = 0.1;
                y[k++] = 0.22;
                r[k] = 0.045;
                y[k++] = 0.5;
                r[k] = 0.07;
                y[k++] = 0.62;
                KitRound.LatheScratch(m, x, yy, z, k, 6, c, 45);
            }
            for (int i = 0; i < count; i += 6)
            {
                double u = u0 + (u1 - u0) * (i + 0.5) / count;
                KitRound.BoxV(m, f, u - 0.13, u + 0.13, v, v + 0.86, -0.05, 0.16, 0.03, 1, BoxFaces.All & ~BoxFaces.Bottom, c);
                double x, yy, z;
                f.ToWorld(u, v + 0.86, 0.05, out x, out yy, out z);
                KitRound.Ellipsoid(m, x, yy + 0.16, z, 0.12, 0.16, 0.12, 7, 4, MeshColor.FromHex(0xE0B13E));
            }
            FacadeKit.Band(m, f, u0, u1, v + 0.74, 0.12, 0.18, 0.03, 3, c);
            Fixed(ref h, f, m, v0, MaterialChannel.Plaster);
        }

        /// <summary>A point inside the plot's display polygon at front-frame (u, w), pulled toward the plot's centre
        /// until inside.</summary>
        private static bool Spot(ref House h, Scratch s, ref Plot p, double u, double w, out double x, out double z)
        {
            double y;
            h.F.ToWorld(u, 0, w, out x, out y, out z);
            double cx, cy, cz;
            h.F.ToWorld(0.5 * (p.U0 + p.U1), 0, -0.5 * p.Depth, out cx, out cy, out cz);
            for (int k = 0; k < 5; k++)
            {
                if (InsideDisplay(s, ref p, x, z, 0.5)) return true;
                x = 0.5 * (x + cx);
                z = 0.5 * (z + cz);
            }
            return InsideDisplay(s, ref p, x, z, 0.5);
        }

        /// <summary>True when (x, z) is inside the display polygon at least <paramref name="margin"/> from its edges.</summary>
        private static bool InsideDisplay(Scratch s, ref Plot p, double x, double z, double margin)
        {
            int at = p.DispStart, n = p.DispCount;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double zi = s.DZ[at + i], zj = s.DZ[at + j], xi = s.DX[at + i], xj = s.DX[at + j];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            if (!inside) return false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (Plane2.PointSeg(x, z, s.DX[at + j], s.DZ[at + j], s.DX[at + i], s.DZ[at + i]) < margin) return false;
            return true;
        }

        /// <summary>
        /// Roof props of a flat terrace (W2_DESIGN 2.6, ref_buildings.md): the stair cabin (mumty) on every roof of
        /// three or more storeys, black and blue tanks (often on the cabin), the evacuated-tube solar heater facing
        /// south, rebar stubs on the column heads of growing houses, a dish, pot plants along the parapet, a laundry
        /// line, a CGI shed on some, and the rooftop restaurant (umbrellas, chairs, plants, prayer flags) in Thamel
        /// and on the Boudha kora.
        /// </summary>
        private static void Props(ref House h, Scratch s, ref Plot p, ref GrammarRng rng0, double y, MeshData m)
        {
            var rng = new GrammarRng(p.Seed, PurposeProps);
            bool house = p.Arch == BuildingArchetype.ModernUrban || p.Arch == BuildingArchetype.NewarHybrid;
            if (!house) return;
            KitFrame f = h.F;
            double area = Math.Abs(SignedArea(s.DX, s.DZ, p.DispStart, p.DispCount));
            if (area < 6) return;
            double uMid = 0.5 * (p.U0 + p.U1), pw = p.U1 - p.U0;
            bool terrace = h.Style.RoofTerraceShare > 0 && rng.Chance(h.Style.RoofTerraceShare);
            bool cabin = p.Storeys >= 3 && area > 14;
            double cabX = 0, cabZ = 0, cabTop = y, cabHalf = 0;
            int v0;
            if (cabin)
            {
                double side = Math.Max(1.9, Math.Min(3.2, Math.Sqrt(rng.Range(0.08f, 0.15f) * area)));
                double cu = rng.Chance(0.5f) ? p.U0 + 0.5 * side + 0.2 : p.U1 - 0.5 * side - 0.2;
                if (pw < side + 0.6) cu = uMid;
                if (Spot(ref h, s, ref p, cu, -p.Depth + 0.5 * side + 0.25, out cabX, out cabZ))
                {
                    cabHalf = 0.5 * side;
                    var cab = new KitFrame(cabX, y, cabZ, f.UX, f.UZ);
                    uint cc = p.Front;
                    v0 = m.VertexCount;
                    int seg = Math.Min(1, h.Det.Segs);
                    KitRound.BoxV(m, cab, -cabHalf, cabHalf, 0, 2.35, -cabHalf, cabHalf, 0.06, seg, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right, cc);
                    KitPaint.Of(p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.ExposedBrick ? MaterialChannel.Plaster : p.FrontCh)
                        .WithGround(y, 0.55f, 0.8f).Apply(m, v0);
                    v0 = m.VertexCount;
                    var cabFront = new KitFrame(cabX, y, cabZ, f.UX, f.UZ).Offset(0, 0, cabHalf);
                    cabFront = new KitFrame(cabFront.OX, y, cabFront.OZ, f.UX, f.UZ);
                    MeshKit.Panel(m, cabFront, -0.42, 0, 0.42, 2.0, 0.01, BuildingGrammar.SteelDark);
                    MeshKit.Box(m, cabFront, -0.48, 0.48, 2.0, 2.08, 0, 0.05, p.Trim, BoxFaces.Wall | BoxFaces.Bottom);
                    KitPaint.Of(MaterialChannel.Metal).Apply(m, v0);
                    v0 = m.VertexCount;
                    KitRound.BoxV(m, cab, -cabHalf - 0.15, cabHalf + 0.15, 2.35, 2.5, -cabHalf - 0.15, cabHalf + 0.15, 0.05, seg, BoxFaces.All, p.Trim);
                    KitPaint.Of(MaterialChannel.Concrete).Apply(m, v0);
                    cabTop = y + 2.5;
                    if (h.Colliders != null)
                        h.Colliders.AddBox(cabX, cabZ, y, cabTop, cabHalf, cabHalf, f.UX, f.UZ, GenColliderFlags.Walkable, GenColliders.Concrete);
                }
                else cabin = false;
            }
            if (!h.Det.Props) return;

            // Water tanks: 60-80% of roofs, 1-3, black first, on the cabin on 40%.
            float tankShare = BuildingGrammar.IsNewarProfile(h.Plan.Profile) ? 0.62f : 0.78f;
            if (rng.Chance(tankShare))
            {
                int tanks = rng.Chance(0.55f) ? 1 : rng.Chance(0.6f) ? 2 : 3;
                bool onCabin = cabin && rng.Chance(0.45f);
                for (int k = 0; k < tanks; k++)
                {
                    uint tc = BuildingGrammar.Tank[rng.Pick(TankShare)];
                    double rad = 0.5 * rng.Range(0.95f, 1.2f), th = rng.Range(0.95f, 1.35f);
                    double x, z, baseY;
                    if (onCabin && k < 2)
                    {
                        double off = tanks > 1 ? (k == 0 ? -0.55 : 0.55) * Math.Min(1, cabHalf / 0.9) : 0;
                        x = cabX + f.UX * off;
                        z = cabZ + f.UZ * off;
                        baseY = cabTop;
                        if (cabHalf < rad + 0.05) rad = cabHalf - 0.05;
                    }
                    else
                    {
                        if (!Spot(ref h, s, ref p, p.U0 + pw * rng.Range(0.2f, 0.8f), -p.Depth * rng.Range(0.35f, 0.75f), out x, out z)) continue;
                        if (cabin && Math.Abs(x - cabX) < cabHalf + rad + 0.2 && Math.Abs(z - cabZ) < cabHalf + rad + 0.2) continue;
                        baseY = y;
                    }
                    double stand = baseY > y + 0.1 ? 0.15 : rng.Range(0.4f, 1.4f);
                    v0 = m.VertexCount;
                    if (rng.Chance(0.6f) || stand < 0.3) PropKit.Stand(m, x, baseY, baseY + stand, z, rad * 0.8, f.UX, f.UZ, PropKit.Steel);
                    else
                    {
                        MeshKit.OrientedBox(m, x - f.WX * 0.3 * rad, z - f.WZ * 0.3 * rad, baseY, baseY + stand, rad * 0.9, 0.11, f.UX, f.UZ, BuildingGrammar.RawBrick, BoxFaces.All & ~BoxFaces.Bottom);
                        MeshKit.OrientedBox(m, x + f.WX * 0.3 * rad, z + f.WZ * 0.3 * rad, baseY, baseY + stand, rad * 0.9, 0.11, f.UX, f.UZ, BuildingGrammar.RawBrick, BoxFaces.All & ~BoxFaces.Bottom);
                    }
                    KitPaint.Of(MaterialChannel.Metal).WithGround(baseY, 0.6f, 0.4f).Apply(m, v0);
                    v0 = m.VertexCount;
                    PropKit.Tank(m, x, baseY + stand, z, rad, th, h.Det.Segs > 1 ? 10 : 8, tc, MeshColor.Scale(tc, 1.25f));
                    KitPaint.Of(MaterialChannel.Paint).WithGround(baseY + stand, 0.7f, 0.3f).Apply(m, v0);
                }
            }
            // Solar water heater facing south.
            if (rng.Chance(BuildingGrammar.IsNewarProfile(h.Plan.Profile) ? 0.15f : 0.24f))
            {
                double x, z;
                if (Spot(ref h, s, ref p, uMid + (rng.Next() - 0.5) * 0.4 * pw, -p.Depth * 0.4, out x, out z) &&
                    (!cabin || Math.Abs(x - cabX) > cabHalf + 1.3 || Math.Abs(z - cabZ) > cabHalf + 1.3))
                {
                    v0 = m.VertexCount;
                    PropKit.SolarHeater(m, x, y, z, 0, -1, 1.6, h.Det.Small ? 14 : 8, rng.Range(32f, 42f), MeshColor.FromHex(0xD9DDE0),
                                        MeshColor.FromHex(0x2B3A4A), PropKit.Galvanised);
                    KitPaint.Of(MaterialChannel.Metal).WithGround(y, 0.6f, 0.5f).Apply(m, v0);
                }
            }
            // Rebar stubs on the column heads of a growing house.
            if (p.Arch == BuildingArchetype.ModernUrban && (p.Storeys >= 3 && rng.Chance(0.42f)))
            {
                v0 = m.VertexCount;
                int cols = Math.Max(2, Math.Min(4, (int)Math.Round(pw / 3.2) + 1));
                for (int rowI = 0; rowI < 2; rowI++)
                    for (int c = 0; c < cols; c++)
                    {
                        double x, z;
                        double u = p.U0 + 0.3 + (pw - 0.6) * c / (cols - 1), w = rowI == 0 ? -0.3 : -p.Depth + 0.3;
                        if (!Spot(ref h, s, ref p, u, w, out x, out z)) continue;
                        PropKit.RebarColumn(m, x, y, z, f.UX, f.UZ, rng.Range(0.1f, 0.4f), rng.Range(0.6f, 1.0f), BuildingGrammar.Concrete, BuildingGrammar.Rust,
                                            BuildingGrammar.Cloth[rng.Int(0, BuildingGrammar.Cloth.Length - 1)], h.Det.Small && rng.Chance(0.35f));
                    }
                KitPaint.Of(MaterialChannel.Metal).WithGround(y, 0.6f, 0.4f).Apply(m, v0);
            }
            // Satellite dish.
            if (rng.Chance(0.15f))
            {
                double x, z;
                if (Spot(ref h, s, ref p, p.U1 - 0.6, -0.6, out x, out z))
                {
                    v0 = m.VertexCount;
                    PropKit.Dish(m, x, y, z, 0.3, -0.95, rng.Range(0.6f, 0.9f), MeshColor.FromHex(0xE6E6E6), PropKit.Steel);
                    KitPaint.Of(MaterialChannel.Metal).Apply(m, v0);
                }
            }
            if (!h.Det.Small) return;
            // Pot plants along the front parapet.
            if (rng.Chance(terrace ? 0.9f : 0.32f))
            {
                int pots = rng.Int(2, terrace ? 6 : 4);
                v0 = m.VertexCount;
                for (int k = 0; k < pots; k++)
                {
                    double x, z;
                    if (!Spot(ref h, s, ref p, p.U0 + 0.4 + (pw - 0.8) * (k + 0.5) / pots, -0.45 + p.Cant, out x, out z)) continue;
                    uint flower = rng.Chance(0.5f) ? (rng.Chance(0.5f) ? BuildingGrammar.Marigold : MeshColor.FromHex(0xE85D9E)) : 0u;
                    PropKit.PottedPlant(m, x, y, z, rng.Range(0.25f, 0.4f), PropKit.Terracotta, rng.Chance(0.5f) ? BuildingGrammar.Foliage : BuildingGrammar.FoliageLight, flower);
                }
                KitPaint.Of(MaterialChannel.Foliage).WithGround(y, 0.6f, 0.3f).Apply(m, v0);
            }
            // Laundry line across the terrace.
            if (rng.Chance(0.6f) && pw > 2.5)
            {
                double ax, az, bx, bz;
                if (Spot(ref h, s, ref p, p.U0 + 0.4, -0.5 * p.Depth, out ax, out az) && Spot(ref h, s, ref p, p.U1 - 0.4, -0.5 * p.Depth - 0.5, out bx, out bz))
                {
                    v0 = m.VertexCount;
                    KitRound.Rod(m, ax, y, az, ax, y + 1.8, az, 0.025, 4, false, PropKit.Steel);
                    KitRound.Rod(m, bx, y, bz, bx, y + 1.8, bz, 0.025, 4, false, PropKit.Steel);
                    PropKit.Laundry(m, ax, y + 1.75, az, bx, y + 1.75, bz, rng.Int(3, 7), BuildingGrammar.Cloth, p.Seed, MeshColor.FromHex(0xDDDDDD));
                    KitPaint.Of(MaterialChannel.Fabric).WithGround(y, 0.7f, 0.6f).Apply(m, v0);
                }
            }
            // Haystacks and grain drying on village terraces (Bungamati, Khokana, the rim).
            if ((h.Plan.Profile == StyleProfile.Bungamati || h.Plan.Profile == StyleProfile.Khokana || h.Plan.Profile == StyleProfile.Rim) && rng.Chance(0.25f))
            {
                double x, z;
                if (Spot(ref h, s, ref p, p.U0 + pw * rng.Range(0.3f, 0.7f), -p.Depth * 0.6, out x, out z))
                {
                    v0 = m.VertexCount;
                    double r = rng.Range(0.7f, 1.1f);
                    KitRound.Ellipsoid(m, x, y + 0.75 * r, z, r, 0.85 * r, r, 8, 5, BuildingGrammar.Hay);
                    KitRound.Ellipsoid(m, x, y + 1.5 * r, z, 0.35 * r, 0.3 * r, 0.35 * r, 6, 3, MeshColor.Scale(BuildingGrammar.Hay, 0.8f));
                    KitPaint.Of(MaterialChannel.Foliage).WithGround(y, 0.6f, 0.6f).Apply(m, v0);
                }
            }
            // A CGI shed over part of the terrace (the "half storey" of the metro).
            if (!terrace && p.Arch == BuildingArchetype.ModernUrban && rng.Chance(0.22f) && pw > 3.2)
            {
                double x, z;
                if (Spot(ref h, s, ref p, uMid, -0.5 * p.Depth, out x, out z))
                {
                    v0 = m.VertexCount;
                    var sf = new KitFrame(x, y, z, f.UX, f.UZ);
                    double hw = Math.Min(2.2, 0.4 * pw), hd = Math.Min(1.8, 0.3 * p.Depth);
                    for (int k = 0; k < 4; k++)
                    {
                        double su = (k & 1) == 0 ? -hw : hw, sw = (k & 2) == 0 ? -hd : hd;
                        double px, py, pz;
                        sf.ToWorld(su, 0, sw, out px, out py, out pz);
                        KitRound.Rod(m, px, y, pz, px, y + (sw > 0 ? 2.4 : 2.1), pz, 0.04, 4, false, PropKit.Steel);
                    }
                    uint cgi = BuildingGrammar.Cgi[rng.Int(0, BuildingGrammar.Cgi.Length - 1)];
                    FacadeKitSheet(m, sf, -hw - 0.2, hw + 0.2, -hd - 0.25, hd + 0.25, 2.05, 2.45, cgi);
                    KitPaint.Of(MaterialChannel.Metal).WithGround(y, 0.6f, 0.6f).Apply(m, v0);
                }
            }
            // Rooftop restaurant.
            if (terrace)
            {
                int umbrellas = rng.Int(1, 3);
                v0 = m.VertexCount;
                for (int k = 0; k < umbrellas; k++)
                {
                    double x, z;
                    if (!Spot(ref h, s, ref p, p.U0 + pw * (k + 1) / (umbrellas + 1), -0.35 * p.Depth, out x, out z)) continue;
                    if (cabin && Math.Abs(x - cabX) < cabHalf + 1.3 && Math.Abs(z - cabZ) < cabHalf + 1.3) continue;
                    PropKit.Umbrella(m, x, y, z, 1.15, 2.0, BuildingGrammar.Sign[rng.Int(0, 3)], MeshColor.FromHex(0xDDDDDD), MeshColor.FromHex(0xF4F1EA));
                    for (int c = 0; c < 2; c++)
                    {
                        double side = c == 0 ? -0.75 : 0.75;
                        PropKit.Chair(m, x + f.UX * side, y, z + f.UZ * side, c == 0 ? f.UX : -f.UX, c == 0 ? f.UZ : -f.UZ,
                                      rng.Chance(0.5f) ? MeshColor.FromHex(0xF4F1EA) : MeshColor.FromHex(0xE8483A));
                    }
                }
                KitPaint.Of(MaterialChannel.Fabric).WithGround(y, 0.6f, 0.5f).Apply(m, v0);
                double ax, az, bx, bz;
                if ((h.Plan.Profile == StyleProfile.Thamel || h.Plan.Profile == StyleProfile.BoudhaKora) && rng.Chance(0.6f) &&
                    Spot(ref h, s, ref p, p.U0 + 0.3, -0.3, out ax, out az) && Spot(ref h, s, ref p, p.U1 - 0.3, -p.Depth + 0.3, out bx, out bz))
                {
                    v0 = m.VertexCount;
                    double topY = cabin ? cabTop + 0.4 : y + 2.6;
                    PropKit.PrayerFlags(m, ax, y + 1.1, az, cabin ? cabX : bx, topY, cabin ? cabZ : bz, MeshColor.FromHex(0xEDE6D6));
                    KitPaint.Of(MaterialChannel.Fabric).Apply(m, v0);
                }
            }
        }

        private static readonly float[] TankShare = { 55, 25, 15, 5 };

        /// <summary>A corrugated sheet roof (skillion) in a frame, sloping down from the back (w = w0) to the front.</summary>
        private static void FacadeKitSheet(MeshData m, in KitFrame f, double u0, double u1, double w0, double w1, double vFront, double vBack, uint c)
        {
            int ribs = Math.Max(4, (int)((u1 - u0) / 0.15));
            for (int i = 0; i < ribs; i++)
            {
                double a = u0 + (u1 - u0) * i / ribs, b = u0 + (u1 - u0) * (i + 1) / ribs, mid = 0.5 * (a + b);
                double ax, ay, az, bx, by, bz, mx, my, mz, ax2, ay2, az2, bx2, by2, bz2, mx2, my2, mz2;
                f.ToWorld(a, vBack, w0, out ax, out ay, out az);
                f.ToWorld(mid, vBack + 0.025, w0, out mx, out my, out mz);
                f.ToWorld(b, vBack, w0, out bx, out by, out bz);
                f.ToWorld(a, vFront, w1, out ax2, out ay2, out az2);
                f.ToWorld(mid, vFront + 0.025, w1, out mx2, out my2, out mz2);
                f.ToWorld(b, vFront, w1, out bx2, out by2, out bz2);
                KitRound.QuadSmooth(m, ax, ay, az, mx, my, mz, mx2, my2, mz2, ax2, ay2, az2, -0.4 * f.UX, 1, -0.4 * f.UZ, 0, 1, 0, 0, 1, 0, -0.4 * f.UX, 1, -0.4 * f.UZ, c);
                KitRound.QuadSmooth(m, mx, my, mz, bx, by, bz, bx2, by2, bz2, mx2, my2, mz2, 0, 1, 0, 0.4 * f.UX, 1, 0.4 * f.UZ, 0.4 * f.UX, 1, 0.4 * f.UZ, 0, 1, 0, c);
            }
            MeshKit.QuadLocal(m, f, u0, vBack - 0.01, w0, u1, vBack - 0.01, w0, u1, vFront - 0.01, w1, u0, vFront - 0.01, w1, 0, -1, 0, MeshColor.Scale(c, 0.7f));
        }
    }
}
