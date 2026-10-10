using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// The palace fronts, gates, the palace tower, the Kal Bhairav relief and the bahal courtyards of the hero set, each
    /// built to its photographed signature (docs/research/w2/ref_temples.md §5 and the per-object briefs):
    /// <list type="bullet">
    /// <item>Gaddi Baithak: a white neoclassical block on a moulded podium with a front loggia of giant Corinthian
    /// columns in pairs, round-arched openings behind, a full entablature, a balustraded parapet with urns and a
    /// pediment with its cartouche; pilasters and tall green-shuttered windows round the sides;</item>
    /// <item>the 55-Window Palace: brick storeys under a tile roof with struts, its top floor one continuous carved
    /// wooden gallery of 55 windows;</item>
    /// <item>the Golden Gate: a red gatehouse with a gilt frame, carved gilt jambs, the gilt torana with Taleju and
    /// Garuda, flanking plaques and a gilt canopy roof with three pinnacles, lions and pennants, set in white walls;
    /// Hanuman Dhoka: the gilt door in the painted palace front with Hanuman under his parasol and the painted lions;</item>
    /// <item>the Basantapur tower: plain brick lower storeys with carved windows, then four real roof tiers, each a
    /// lattice gallery storey under a wide tiled roof on struts;</item>
    /// <item>Kal Bhairav: the six-armed painted relief in its arched stone niche, crowned and garlanded, trampling a
    /// figure, flanked by white lions;</item>
    /// <item>courtyards (Kumari Ghar, Kwa Bahal): ranges of carved projecting windows round a paved court under one
    /// continuous roof with a red fringe.</item>
    /// </list>
    /// </summary>
    public static partial class HeroForms
    {
        private static readonly ShapeBrush Stucco = new ShapeBrush(MeshColor.FromHex(0xF8F5EC), MaterialChannel.Plaster);
        private static readonly ShapeBrush StuccoTrim = new ShapeBrush(MeshColor.FromHex(0xFFFFFF), MaterialChannel.Plaster);
        private static readonly ShapeBrush Shutter = new ShapeBrush(MeshColor.FromHex(0x4E7D52), MaterialChannel.Paint);
        private static readonly ShapeBrush DarkGlass = new ShapeBrush(MeshColor.FromHex(0x34404A), MaterialChannel.Glass);
        private static readonly ShapeBrush GateRed = new ShapeBrush(MeshColor.FromHex(0xB32621), MaterialChannel.Paint);
        private static readonly ShapeBrush NewarBrick = new ShapeBrush(MeshColor.FromHex(0xA44A33), MaterialChannel.BrickGlazed);

        // ---------------------------------------------------------------------------------------------------------
        // Gaddi Baithak and the Newar palace range
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A palace block: <paramref name="rana"/> = the neoclassical Gaddi Baithak; else a Newar palace range with the
        /// continuous carved gallery of <paramref name="upperWindows"/> windows on its top floor (55-Window Palace).
        /// </summary>
        public static int Palace(bool rana, float w, float d, float h, int storeys, int upperWindows, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            int windows = rana ? RanaPalace(m, k, w, d, h, lod) : WindowPalace(m, k, w, d, h, Math.Max(2, storeys), upperWindows, lod);
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY, f.GroundY + h, 0.5 * w, 0.5 * d, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = h;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>Gaddi Baithak (W2_DESIGN 3.4 #16, photos refs/temples/gaddi2): returns the front windows.</summary>
        private static int RanaPalace(MeshData m, in Affine3 k, double w, double d, double h, int lod)
        {
            double hw = 0.5 * w, hd = 0.5 * d;
            double podium = Math.Min(1.8, 0.13 * h), parapet = 1.25, entab = 0.9;
            double orderTop = h - parapet - entab, entTop = h - parapet;
            double loggia = 0.32 * w, recess = 2.4;
            if (lod >= 2)
            {
                SacredDraw.Box(m, k, -hw, hw, -0.3, entTop, -hd, hd, Stucco, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, k, -0.7 * loggia, 0.7 * loggia, entTop, h, hd - 0.6, hd, StuccoTrim, BoxFaces.All & ~BoxFaces.Bottom);
                for (int q = -2; q <= 2; q++) SacredDraw.Box(m, k, q * 0.4 * loggia - 0.4, q * 0.4 * loggia + 0.4, podium, orderTop, hd, hd + 0.5, StuccoTrim, BoxFaces.Wall);
                return 0;
            }
            // Podium with its band of pierced panels, a broad stair to the loggia.
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hw, hd, pu, pw);
            Mould mo = SacredDraw.M;
            mo.Add(0.35, -0.3, StuccoTrim).Add(0.35, 0.3).Add(0.25, 0.4).Add(0.25, podium - 0.35, Stucco).Add(0.35, podium - 0.25, StuccoTrim).Add(0.35, podium);
            SacredDraw.Ring(m, k, pu, pw, n, mo);
            SacredDraw.CapRect(m, k, 0, 0, hw + 0.35, hd + 0.35, podium, true, Looks.Of(MeshColor.FromHex(0xE4DED2), MaterialChannel.Flagstone));
            if (lod == 0)
            {
                ShapeBrush pierce = Looks.Of(MeshColor.FromHex(0xD9D2C3), MaterialChannel.Plaster, 0.7f);
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                    double along = sd % 2 == 0 ? hw : hd, wall = (sd % 2 == 0 ? hd : hw) + 0.25;
                    int panels = (int)(2 * along / 1.2);
                    for (int q = 0; q < panels; q++)
                    {
                        double u = -along + 2 * along * (q + 0.5) / panels;
                        SacredDraw.Panel(m, side, u - 0.42, 0.55, u + 0.42, podium - 0.45, wall + 0.005, pierce);
                    }
                }
            }
            SacredParts.Stair(m, null, default, k, 0.5 * loggia, hd + 0.35, podium / Math.Tan(33 * Math.PI / 180), 0, podium, 0.4, 0.5, Looks.StoneLight, Stucco, lod,
                              GenColliders.Stone);
            // The block: outer walls from the podium to the entablature; the loggia's wall set back behind the columns.
            BlockWalls(m, k, -hw, hw, -hd, hd - recess, podium, entTop, Stucco);
            BlockWalls(m, k, -hw, -loggia, hd - recess, hd, podium, entTop, Stucco);
            BlockWalls(m, k, loggia, hw, hd - recess, hd, podium, entTop, Stucco);
            SacredDraw.CapRect(m, k, 0, hd - 0.5 * recess, loggia, 0.5 * recess, orderTop, false, Looks.Ao(Stucco, 0.6f));
            SacredDraw.CapRect(m, k, 0, -0.5 * recess, hw, hd - 0.5 * recess, entTop, true, Stucco);
            // Entablature round the whole block and over the loggia.
            n = SacredDraw.Rect(0, 0, hw, hd, pu, pw);
            Mould en = SacredDraw.M;
            en.Add(0, orderTop, StuccoTrim).Add(0.08, orderTop + 0.1).Add(0.08, orderTop + 0.35).Add(0.06, orderTop + 0.35, Stucco).Add(0.06, entTop - 0.35)
              .Add(0.2, entTop - 0.3, StuccoTrim).Add(0.45, entTop - 0.12).Add(0.5, entTop);
            SacredDraw.Ring(m, k, pu, pw, n, en);
            SacredDraw.Box(m, k, -loggia, loggia, orderTop, entTop, hd - 0.2, hd + 0.3, StuccoTrim, BoxFaces.Front | BoxFaces.Bottom);
            // Columns: pairs at the loggia ends and either side of the centre, singles between.
            double colR = Math.Min(0.45, 0.012 * w), colH = orderTop - podium;
            double[] cols = { -1.0, -0.93, -0.62, -0.31, -0.24, 0.24, 0.31, 0.62, 0.93, 1.0 };
            for (int q = 0; q < cols.Length; q++) ClassicColumn(m, k, cols[q] * (loggia - colR - 0.1), podium, hd + 0.05, colH, colR, lod);
            // The loggia's arched openings in two rows behind the columns.
            int bays = 5;
            for (int row = 0; row < 2; row++)
            {
                double vb = podium + 0.5 + row * 0.5 * colH, wh = 0.5 * colH - 1.4;
                for (int q = 0; q < bays; q++)
                {
                    double u = -loggia + 2 * loggia * (q + 0.5) / bays;
                    ArchedOpening(m, k, u, vb, hd - recess, Math.Min(2.2, 1.4 * loggia / bays), wh, lod);
                }
            }
            // Pilasters and tall shuttered windows round the rest of the block (two rows).
            int windows = 0;
            for (int sd = 0; sd < 4; sd++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                double along = sd % 2 == 0 ? hw : hd, wall = sd % 2 == 0 ? hd : hw;
                int bayN = Math.Max(2, (int)Math.Round(2 * along / 3.6));
                for (int q = 0; q <= bayN; q++)
                {
                    double u = -along + 2 * along * q / bayN;
                    if (sd == 0 && Math.Abs(u) < loggia + 0.5) continue;
                    if (lod == 0) Shapes.RoundedBox(m, SacredDraw.At(side, u, podium + 0.5 * colH, wall + 0.12), StuccoTrim, 0.55, colH, 0.24, 0.04, 1);
                }
                for (int q = 0; q < bayN; q++)
                {
                    double u = -along + 2 * along * (q + 0.5) / bayN;
                    if (sd == 0 && Math.Abs(u) < loggia + 0.8) continue;
                    for (int row = 0; row < 2; row++)
                    {
                        double wb = podium + 0.7 + row * 0.5 * colH, wt = wb + 0.5 * colH - 1.6;
                        ShutteredWindow(m, side, u, wb, wt, wall, lod);
                        if (sd == 0) windows++;
                    }
                }
            }
            // Parapet: a balustrade along the front over the loggia (panels elsewhere), urns over the column pairs.
            Mould pm = SacredDraw.M;
            pm.Add(0.3, entTop, Stucco).Add(0.3, h - 0.2).Add(0.38, h - 0.15, StuccoTrim).Add(0.38, h).Add(0.1, h).Add(0.1, entTop);
            n = SacredDraw.Rect(0, 0, hw, hd, pu, pw);
            if (lod == 0)
            {
                // The front run of the parapet is open balusters over the loggia.
                SacredDraw.Ring(m, k, pu, pw, n, pm);
                Balustrade(m, k, -loggia, loggia, hd + 0.35, entTop, h - 0.2, 0.42, lod);
            }
            else
            {
                SacredDraw.Ring(m, k, pu, pw, n, pm);
            }
            foreach (double cu in new[] { -0.965, -0.275, 0.275, 0.965 })
                Urn(m, k, cu * (loggia - colR - 0.1), h, hd + 0.2, 0.32, lod);
            // Pediment over the central bays: triangle, raking cornice, cartouche and acroteria.
            // The pediment rises from the entablature over the central bays to the full height.
            double pw2 = 0.46 * loggia, ph = h - entTop;
            double[] tx = ShapeScratch.U, tz = ShapeScratch.W;
            tx[0] = -pw2;
            tz[0] = 0;
            tx[1] = pw2;
            tz[1] = 0;
            tx[2] = 0;
            tz[2] = -ph;
            double pv = h - ph;
            Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, pv, hd + 0.1, 1, 0, 0, 0, 0, 1), Stucco, tx, tz, 3, 0.4, 0.05, 1, BevelStyle.Chamfer, true, false);
            if (lod == 0)
            {
                Shapes.Bar(m, k, StuccoTrim, -pw2 - 0.1, pv, hd + 0.55, 0, h + 0.05, hd + 0.55, 0.1, 4);
                Shapes.Bar(m, k, StuccoTrim, pw2 + 0.1, pv, hd + 0.55, 0, h + 0.05, hd + 0.55, 0.1, 4);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, pv + 0.38 * ph, hd + 0.55), StuccoTrim, 0.55, 0.35, 0.12, 10, false);
            }
            Urn(m, k, 0, h - 0.05, hd + 0.3, 0.3, lod);
            return windows;
        }

        /// <summary>Four flat walls of a block from (u0, w0) to (u1, w1), v0 to v1 (no top).</summary>
        private static void BlockWalls(MeshData m, in Affine3 k, double u0, double u1, double w0, double w1, double v0, double v1, in ShapeBrush b)
        {
            SacredDraw.Box(m, k, u0, u1, v0, v1, w0, w1, b, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
        }

        /// <summary>A Corinthian column on the plane w (proud of it) at u from v0, height h, radius r: moulded base, a
        /// shaft with entasis, a flaring capital with an abacus.</summary>
        private static void ClassicColumn(MeshData m, in Affine3 k, double u, double v0, double w, double h, double r, int lod)
        {
            Profile2 p = Prof;
            p.Add(0, 0).Add(1.45 * r, 0, true).Add(1.45 * r, 0.12 * r, true).Add(1.25 * r, 0.22 * r).Add(1.3 * r, 0.32 * r).Add(1.08 * r, 0.42 * r, true)
             .Add(1.0 * r, 0.5 * r).Add(1.03 * r, 0.35 * h).Add(0.86 * r, h - 1.6 * r, true).Add(0.95 * r, h - 1.45 * r)
             .Add(1.25 * r, h - 0.7 * r).Add(1.45 * r, h - 0.3 * r, true).Add(1.45 * r, h, true).Add(0, h);
            Shapes.Lathe(m, SacredDraw.At(k, u, v0, w), StuccoTrim, p, lod == 0 ? 10 : 6);
            if (lod == 0)
            {
                // Acanthus leaves round the capital.
                for (int i = 0; i < 5; i++)
                {
                    double a = 2 * Math.PI * i / 5;
                    Shapes.Ellipsoid(m, SacredDraw.At(k, u + 1.05 * r * Math.Sin(a), v0 + h - 1.0 * r, w + 1.05 * r * Math.Cos(a)), Stucco, 0.3 * r, 0.45 * r, 0.3 * r, 4, false);
                }
            }
        }

        /// <summary>A round-arched opening (door or window) on the plane w: a dark glazed recess with a white
        /// architrave ring and a keystone.</summary>
        private static void ArchedOpening(MeshData m, in Affine3 k, double u, double v, double w, double ww, double wh, int lod)
        {
            double r = 0.5 * ww, spring = v + wh - r;
            SacredDraw.Panel(m, k, u - r, v, u + r, spring, w + 0.01, DarkGlass);
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            int seg = lod == 0 ? 10 : 5, n = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg;
                x[n] = r * Math.Cos(a);
                z[n++] = -r * Math.Sin(a);
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(k, u, spring, w + 0.005, 1, 0, 0, 0, 0, 1), DarkGlass, x, z, n, 0.01, 0, 0, BevelStyle.Chamfer, true, false);
            if (lod == 0)
            {
                Affine3 ring = Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(k, u, spring, w + 0.05));
                Shapes.Torus(m, ring, StuccoTrim, r + 0.08, 0.08, 12, 4, default, -90, 180, true);
                Shapes.RoundedBox(m, SacredDraw.At(k, u, spring + r + 0.1, w + 0.08), StuccoTrim, 0.22, 0.3, 0.14, 0.03, 1);
                // Glazing bars.
                SacredDraw.Box(m, k, u - 0.03, u + 0.03, v, spring + r - 0.05, w + 0.01, w + 0.04, StuccoTrim, BoxFaces.Front);
            }
        }

        /// <summary>A tall window with a white frame, a hood cornice and green louvred shutters folded back.</summary>
        private static void ShutteredWindow(MeshData m, in Affine3 side, double u, double wb, double wt, double half, int lod)
        {
            SacredDraw.Box(m, side, u - 0.78, u + 0.78, wb - 0.1, wt + 0.1, half, half + 0.1, StuccoTrim, lod == 0 ? BoxFaces.Wall : BoxFaces.Front);
            SacredDraw.Panel(m, side, u - 0.6, wb, u + 0.6, wt, half + 0.105, DarkGlass);
            if (lod > 0) return;
            SacredDraw.Panel(m, side, u - 0.92, wb, u - 0.64, wt, half + 0.12, Shutter);
            SacredDraw.Panel(m, side, u + 0.64, wb, u + 0.92, wt, half + 0.12, Shutter);
            SacredDraw.Box(m, side, u - 0.98, u + 0.98, wt + 0.1, wt + 0.3, half, half + 0.26, StuccoTrim, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod == 0) SacredDraw.Box(m, side, u - 0.03, u + 0.03, wb, wt, half + 0.105, half + 0.13, StuccoTrim, BoxFaces.Front);
        }

        /// <summary>A balustrade along local u from u0 to u1 on the plane w: plinth rail, turned balusters at a pitch and a
        /// hand rail.</summary>
        private static void Balustrade(MeshData m, in Affine3 k, double u0, double u1, double w, double v0, double v1, double pitch, int lod)
        {
            double t = 0.24, rail = 0.16;
            SacredDraw.Box(m, k, u0, u1, v0, v0 + rail, w - t, w + t, StuccoTrim, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, k, u0, u1, v1 - rail, v1, w - t, w + t, StuccoTrim, BoxFaces.All);
            double bh = v1 - v0 - 2 * rail;
            Profile2 p = Prof;
            p.Add(0, 0).Add(0.09, 0, true).Add(0.07, 0.1 * bh).Add(0.12, 0.35 * bh).Add(0.06, 0.7 * bh).Add(0.05, 0.85 * bh).Add(0.09, bh, true).Add(0, bh);
            int count = Math.Max(2, (int)((u1 - u0) / pitch));
            int first = m.VertexCount, fi = m.IndexCount;
            Shapes.Lathe(m, SacredDraw.At(k, u0 + 0.5 * pitch, v0 + rail, w), Stucco, p, 6);
            int nv = m.VertexCount - first, ni = m.IndexCount - fi;
            for (int q = 1; q < count; q++)
            {
                double du = (u1 - u0 - pitch) * q / (count - 1), ox, oy, oz;
                k.Vector(du, 0, 0, out ox, out oy, out oz);
                Shapes.CopyTransformed(m, m, Affine3.Translation(ox, oy, oz), 0xFFFFFFFFu, first, nv, fi, ni);
            }
        }

        /// <summary>A stucco urn (lathe) standing on (u, v, w), height h.</summary>
        private static void Urn(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            Profile2 p = Prof;
            p.Add(0, 0).Add(0.22 * h, 0, true).Add(0.15 * h, 0.15 * h).Add(0.35 * h, 0.45 * h).Add(0.3 * h, 0.7 * h).Add(0.15 * h, 0.8 * h).Add(0.2 * h, 0.9 * h)
             .Add(0.06 * h, h).Add(0, h);
            Shapes.Lathe(m, SacredDraw.At(k, u, v, w), StuccoTrim, p, lod == 0 ? 10 : 6);
        }

        /// <summary>The 55-Window Palace type: returns the gallery windows.</summary>
        private static int WindowPalace(MeshData m, in Affine3 k, double w, double d, double h, int storeys, int upperWindows, int lod)
        {
            double hw = 0.5 * w, hd = 0.5 * d;
            double roofRise = Math.Min(3.0, 0.25 * d), wallTop = h - roofRise, sH = wallTop / storeys;
            for (int s = 0; s < storeys; s++)
                SacredParts.BandedWall(m, k, hw, hd, s == 0 ? -0.4 : s * sH, (s + 1) * sH, NewarBrick, Looks.WoodDark, lod, true, s == storeys - 1);
            int windows = 0;
            // The lower storeys: paired carved windows and a few doors.
            for (int s = 0; s < storeys - 1; s++)
            {
                int count = Math.Max(1, (int)Math.Round(w / 4.0));
                for (int q = 0; q < count; q++)
                {
                    double u = -hw + w * (q + 0.5) / count;
                    if (s == 0 && q % 3 == 1)
                    {
                        SacredParts.Door(m, k, u, 0, hd, 1.1, Math.Min(2.2, sH - 0.5), Looks.WoodDark, lod + 1, false);
                        continue;
                    }
                    if (lod >= 2)
                    {
                        SacredDraw.Panel(m, k, u - 0.45, s * sH + 0.6, u + 0.45, s * sH + 0.6 + Math.Min(1.3, sH - 1.0), hd + 0.01, Looks.Recess);
                        continue;
                    }
                    // The piano nobile (the storey under the gallery) carries the big carved windows: a latticed
                    // opening between carved wings under a fan-shaped crest; the ground storey keeps plain lattices.
                    bool noble = s == storeys - 2;
                    double ww = noble ? 1.05 : 0.9, wh = Math.Min(noble ? 1.45 : 1.3, sH - 1.0), vc = s * sH + 0.55 * sH;
                    SacredParts.LatticeWindow(m, k, u, vc, hd, ww, wh, Looks.WoodDark, noble ? lod : lod + 1, 3);
                    if (!noble || lod > 0) continue;
                    ShikharaGenerator.Arch(m, k, u, vc + 0.5 * wh + 0.1, hd + 0.02, ww + 0.36, 0.42, 0.07, 0.06, true, Looks.WoodMid, 1);
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        double eu = u + sd * (0.5 * ww + 0.2);
                        SacredDraw.Box(m, k, eu - 0.09, eu + 0.09, vc - 0.45 * wh, vc + 0.35 * wh, hd, hd + 0.08, Looks.WoodMid, BoxFaces.Wall);
                    }
                }
            }
            // The top floor gallery: one projecting carved band holding the windows edge to edge.
            double gv0 = (storeys - 1) * sH + 0.25, gv1 = wallTop - 0.35, gh = gv1 - gv0;
            int gw = upperWindows > 0 ? upperWindows : (int)(w / 0.9);
            double pitch = (w - 0.6) / gw;
            Mould band = SacredDraw.M;
            band.Add(0, gv0 - 0.3, Looks.WoodDark).Add(0.32, gv0 - 0.05).Add(0.32, gv0).Add(0.28, gv0, Looks.Ao(Looks.WoodDark, 0.85f)).Add(0.28, gv1)
                .Add(0.38, gv1 + 0.08, Looks.WoodDark).Add(0.38, gv1 + 0.22).Add(0, gv1 + 0.3);
            double[] bu = ShapeScratch.U2, bw = ShapeScratch.W2;
            int bn = SacredDraw.Rect(0, hd - 0.15, hw - 0.1, 0.15, bu, bw);
            SacredDraw.Ring(m, k, bu, bw, bn, band);
            for (int q = 0; q < gw; q++)
            {
                double u = -hw + 0.3 + pitch * (q + 0.5);
                if (lod >= 2)
                {
                    if (q % 3 == 0) SacredDraw.Panel(m, k, u - 0.3, gv0 + 0.15, u + 0.3, gv1 - 0.15, hd + 0.29, Looks.Recess);
                }
                else
                {
                    // Each window: a dark lattice opening between carved posts, a small arched head.
                    SacredDraw.Panel(m, k, u - 0.36 * pitch, gv0 + 0.18, u + 0.36 * pitch, gv1 - 0.22, hd + 0.29, Looks.Recess);
                    SacredDraw.Box(m, k, u + 0.4 * pitch, u + 0.5 * pitch, gv0, gv1, hd + 0.28, hd + 0.36, Looks.WoodDark, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    if (lod == 0)
                    {
                        SacredDraw.Box(m, k, u - 0.36 * pitch, u + 0.36 * pitch, gv0 + 0.5 * gh - 0.02, gv0 + 0.5 * gh + 0.02, hd + 0.29, hd + 0.32, Looks.WoodDark, BoxFaces.Front);
                        SacredDraw.Box(m, k, u - 0.015, u + 0.015, gv0 + 0.18, gv1 - 0.22, hd + 0.29, hd + 0.32, Looks.WoodDark, BoxFaces.Front);
                        ShikharaGenerator.Arch(m, k, u, gv1 - 0.32, hd + 0.29, 0.72 * pitch, 0.12, 0.05, 0.02, true, Looks.WoodMid, 1);
                    }
                }
                windows++;
            }
            RoofSpec r = SacredParts.HipRoof(m, k, hw, hd, wallTop + 0.3, 1.1, 0.3, h, false, 0, false, lod);
            if (lod <= 1) SacredParts.Struts(m, k, r, wallTop - 0.9, Math.Max(3, (int)(w / 2.2)), 3, false, Looks.WoodDark, 9u, lod);
            return windows;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gates
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A palace gate: <paramref name="golden"/> = Bhaktapur's Golden Gate; else Hanuman Dhoka.</summary>
        public static int Gate(bool golden, float widthM, float heightM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double top = golden ? GoldenGate(m, k, widthM, heightM, lod) : HanumanDhoka(m, k, widthM, heightM, lod);
            double hw = 0.5 * widthM + 1.0, depth = golden ? 1.4 : 2.5;
            if (c != null)
            {
                // The walls either side of the passage are solid.
                double x, y, z, dw = golden ? 1.5 : Math.Min(widthM - 0.6, 2.4);
                kf.ToWorld(-0.5 * hw - 0.25 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + heightM, 0.5 * (hw - 0.5 * dw), 0.5 * depth, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
                kf.ToWorld(0.5 * hw + 0.25 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + heightM, 0.5 * (hw - 0.5 * dw), 0.5 * depth, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
            if (stats != null)
            {
                stats.TopM = (float)top;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>
        /// Bhaktapur's Golden Gate (refs/temples/goldengate2): a bright red gatehouse front in a gilt frame between the white
        /// palace walls; the doorway with carved gilt jambs of stacked deities; the gilt torana with the many-armed Taleju
        /// and Garuda over it; two gilt plaques; the gilt canopy roof with upturned ends, three pinnacles, two lions and
        /// pennants, its finial at <paramref name="heightM"/>. Returns the top.
        /// </summary>
        private static double GoldenGate(MeshData m, in Affine3 k, double widthM, double heightM, int lod)
        {
            double fw = Math.Max(3.6, widthM + 0.6), hw = 0.5 * fw, depth = 1.2;
            double canopyV = 0.71 * heightM, front = 0.0;
            ShapeBrush gilt = Looks.Gilt, giltHi = Looks.GiltHi, lime = Looks.Of(MeshColor.FromHex(0xF4F2EC), MaterialChannel.Plaster);
            // The white palace walls either side.
            SacredParts.BandedWall(m, SacredDraw.At(k, -hw - 6, 0, -0.5 * depth - 0.3), 6, 0.5 * depth, -0.3, canopyV - 0.4, lime, Looks.WoodDark, lod, false, true);
            SacredParts.BandedWall(m, SacredDraw.At(k, hw + 6, 0, -0.5 * depth - 0.3), 6, 0.5 * depth, -0.3, canopyV - 0.4, lime, Looks.WoodDark, lod, false, true);
            // The red front and its gilt frame.
            SacredDraw.Box(m, k, -hw, hw, -0.3, canopyV, -depth, front, GateRed, BoxFaces.All & ~BoxFaces.Bottom);
            double dw = 1.5, dh = Math.Min(2.5, 0.46 * heightM);
            if (lod <= 1)
            {
                SacredDraw.Box(m, k, -hw, -hw + 0.2, 0, canopyV, front, front + 0.06, gilt, BoxFaces.Wall);
                SacredDraw.Box(m, k, hw - 0.2, hw, 0, canopyV, front, front + 0.06, gilt, BoxFaces.Wall);
                SacredDraw.Box(m, k, -hw, hw, canopyV - 0.28, canopyV, front, front + 0.08, gilt, BoxFaces.Wall | BoxFaces.Bottom);
                // Stepped stone threshold.
                SacredDraw.Box(m, k, -hw - 0.3, hw + 0.3, -0.3, 0.18, front, front + 0.6, Looks.Stone, BoxFaces.All & ~BoxFaces.Bottom);
            }
            // Doorway (open passage into the palace) with the carved gilt jambs of stacked deities.
            SacredDraw.Panel(m, k, -0.5 * dw, 0, 0.5 * dw, dh, front + 0.01, Looks.Ao(Looks.Of(MeshColor.FromHex(0xE8E2D4), MaterialChannel.Plaster), 0.6f));
            double jw = 0.42;
            for (int s = -1; s <= 1; s += 2)
            {
                double ju = s * (0.5 * dw + 0.5 * jw);
                SacredDraw.Box(m, k, ju - 0.5 * jw, ju + 0.5 * jw, 0, dh + 0.25, front, front + 0.14, gilt, lod <= 1 ? BoxFaces.Wall : BoxFaces.Front);
                if (lod == 0)
                {
                    for (int q = 0; q < 6; q++)
                    {
                        double fv = 0.3 + (dh - 0.3) * (q + 0.5) / 6;
                        Shapes.Ellipsoid(m, SacredDraw.At(k, ju, fv, front + 0.17), giltHi, 0.11, 0.17, 0.06, 6, false);
                        Shapes.Sphere(m, SacredDraw.At(k, ju, fv + 0.2, front + 0.17), giltHi, 0.06, 5);
                    }
                }
            }
            SacredDraw.Box(m, k, -0.5 * dw - jw, 0.5 * dw + jw, dh + 0.25, dh + 0.42, front, front + 0.2, gilt, BoxFaces.Wall | BoxFaces.Bottom);
            // The great torana with Taleju and Garuda.
            double tv = dh + 0.42, tw = dw + 2 * jw + 0.5;
            double tTop = SacredParts.Torana(m, k, 0, tv, front + 0.02, tw, gilt, lod);
            if (lod == 0)
            {
                // Taleju: a seated many-armed figure in the tympanum; Garuda with spread wings at the crown.
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, tv + 0.35, front + 0.18), giltHi, 0.14, 0.22, 0.08, 8, false);
                Shapes.Sphere(m, SacredDraw.At(k, 0, tv + 0.62, front + 0.19), giltHi, 0.08, 6);
                for (int a = 0; a < 8; a++)
                {
                    double ang = -0.15 * Math.PI + 1.3 * Math.PI * a / 7;
                    Shapes.Bar(m, k, giltHi, 0, tv + 0.38, front + 0.18, 0.32 * Math.Cos(ang), tv + 0.38 + 0.26 * Math.Sin(ang), front + 0.2, 0.025, 4);
                }
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, tTop - 0.1, front + 0.15), giltHi, 0.12, 0.16, 0.1, 6, false);
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Ellipsoid(m, Affine3.RotationZ(s * 0.5).Then(SacredDraw.At(k, s * 0.28, tTop - 0.05, front + 0.12)), giltHi, 0.25, 0.08, 0.03, 6, false);
            }
            // Gilt plaques either side.
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double[] x = ShapeScratch.U, z = ShapeScratch.W;
                    int n = 0, seg = lod == 0 ? 8 : 4;
                    x[n] = -0.32;
                    z[n++] = 0;
                    x[n] = 0.32;
                    z[n++] = 0;
                    for (int i = 0; i <= seg; i++)
                    {
                        double a = Math.PI * i / seg;
                        x[n] = 0.32 * Math.Cos(a);
                        z[n++] = -(0.45 + 0.32 * Math.Sin(a));
                    }
                    Shapes.BevelExtrude(m, SacredDraw.Basis(k, s * (hw - 0.62), 0.42 * heightM, front, 1, 0, 0, 0, 0, 1), gilt, x, z, n, 0.06, 0.02, 1, BevelStyle.Round, true, false);
                }
            }
            // The gilt canopy roof: a curved copper hood with ribs and upturned ends.
            double cw = hw + 0.5, cd = 0.9;
            Profile2 hood = Prof;
            hood.Add(-cd, 0.0).Add(-0.6 * cd, 0.32).Add(0, 0.52).Add(0.6 * cd, 0.32).Add(cd, 0.0);
            Path3 path = new Path3(4);
            path.Add(-cw, canopyV, 0.25 - 0.3 * depth).Add(cw, canopyV, 0.25 - 0.3 * depth);
            Shapes.Sweep(m, k, gilt, path, hood.Smooth(lod == 0 ? 3 : 1), false, true, SweepFrames.Upright);
            if (lod == 0)
            {
                // A thin rim along the hood's front edge hung with a valance of gilt drops, and makara curls at its ends.
                double rimW = 0.25 - 0.3 * depth + cd - 0.02;
                Shapes.Bar(m, k, giltHi, -cw + 0.08, canopyV + 0.03, rimW, cw - 0.08, canopyV + 0.03, rimW, 0.032, 6);
                int drops = (int)((2 * cw - 0.4) / 0.32);
                for (int q = 0; q <= drops; q++)
                {
                    double du = -cw + 0.2 + (2 * cw - 0.4) * q / drops;
                    Shapes.Ellipsoid(m, SacredDraw.At(k, du, canopyV - 0.07, rimW), giltHi, 0.055, 0.1, 0.03, 6, false);
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    Path3 curl = new Path3(4);
                    curl.Add(s * (cw - 0.1), canopyV + 0.05, 0.25 - 0.3 * depth + 0.7 * cd).Add(s * (cw + 0.25), canopyV + 0.12, 0.25 - 0.3 * depth + 0.75 * cd)
                        .Add(s * (cw + 0.38), canopyV + 0.35, 0.25 - 0.3 * depth + 0.6 * cd);
                    Shapes.Tube(m, k, gilt, curl, 0.09, 6, true, false, default, 0.03);
                }
            }
            // Three pinnacles on the hood, the central one in its arched halo; two lions; pennants.
            double baseV = canopyV + 0.5, tip = heightM;
            SacredParts.Gajur(m, SacredDraw.At(k, 0, 0, 0.25 - 0.3 * depth), baseV, tip - baseV, 0.16, gilt, lod);
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredParts.Gajur(m, SacredDraw.At(k, s * 0.75, 0, 0.25 - 0.3 * depth), baseV - 0.05, 0.55 * (tip - baseV), 0.11, gilt, lod + 1);
                    SacredFigures.Guardian(m, k, s * (cw - 0.4), baseV - 0.08, 0.25 - 0.3 * depth, 0.45, GuardianKind.Lion, false, SacredFigures.Small, lod, s);
                    if (lod == 0)
                    {
                        Shapes.Cylinder(m, SacredDraw.At(k, s * 1.3, baseV - 0.1, 0.25 - 0.3 * depth), giltHi, 0.02, tip - baseV - 0.1, 4);
                        SacredDraw.Tri(m, k, s * 1.3, tip - 0.25, 0.25 - 0.3 * depth, s * 1.3, tip - 0.6, 0.25 - 0.3 * depth, s * 1.75, tip - 0.4, 0.25 - 0.3 * depth, 0, 0, 1, giltHi);
                        SacredDraw.Tri(m, k, s * 1.3, tip - 0.25, 0.25 - 0.3 * depth, s * 1.3, tip - 0.6, 0.25 - 0.3 * depth, s * 1.75, tip - 0.4, 0.25 - 0.3 * depth, 0, 0, -1, giltHi);
                    }
                }
                if (lod == 0)
                {
                    Affine3 halo = Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(k, 0, baseV + 0.42 * (tip - baseV), 0.25 - 0.3 * depth));
                    Shapes.Torus(m, halo, giltHi, 0.55, 0.035, 14, 4, default, -100, 200, true);
                }
            }
            return tip;
        }

        /// <summary>Hanuman Dhoka: the gilt door in the painted palace front under a small tile roof, Hanuman wrapped in
        /// red cloth under his red parasol, two painted lions. Returns the top.</summary>
        private static double HanumanDhoka(MeshData m, in Affine3 k, double widthM, double heightM, int lod)
        {
            double hw = 0.5 * widthM + 1.0, depth = 2.5;
            const double RoofH = 1.4;
            double wallH = Math.Max(3.0, heightM - RoofH);
            SacredParts.BandedWall(m, SacredDraw.At(k, 0, 0, -0.5 * depth), hw, 0.5 * depth, -0.3, wallH, Looks.WallGlazed, Looks.WoodDark, lod, true, true);
            double dw = Math.Min(widthM - 0.6, 2.4), dh = Math.Min(wallH - 1.6, 3.2);
            SacredParts.Door(m, k, 0, 0, 0, dw, dh, Looks.Gilt, lod, true, wallH - 0.1);
            SacredDraw.Panel(m, k, -0.5 * dw, 0, 0.5 * dw, dh, 0.013, Looks.Of(SacredPalette.GiltLo, MaterialChannel.Gilt));
            SacredParts.HipRoof(m, SacredDraw.At(k, 0, 0, -0.5 * depth), hw, 0.5 * depth, wallH, 0.7, 0.2, wallH + RoofH, false, 0, true, lod);
            // Hanuman to the left of the gate, wrapped in red under a red parasol; lions either side.
            double hu = -hw - 1.6;
            SacredParts.Pedestal(m, k, hu, 0, 1.2, 0.7, 0.7, 1.0, Looks.Stone, lod);
            ShapeBrush cloth = Looks.Of(MeshColor.FromHex(0xC8202A), MaterialChannel.Fabric);
            Shapes.Ellipsoid(m, SacredDraw.At(k, hu, 1.8, 1.2), cloth, 0.5, 0.85, 0.42, lod == 0 ? 10 : 6, lod == 0);
            Shapes.Ellipsoid(m, SacredDraw.At(k, hu, 2.75, 1.25), Looks.Of(MeshColor.FromHex(0xE05A2B), MaterialChannel.Paint), 0.25, 0.28, 0.25, lod == 0 ? 10 : 6, lod == 0);
            Shapes.Cylinder(m, SacredDraw.At(k, hu, 1.0, 0.9), Looks.Of(MeshColor.FromHex(0x5A5A5A), MaterialChannel.Metal), 0.04, 2.8, 4);
            Profile2 um = Prof;
            um.Add(0, 3.4).Add(1.05, 3.4, true).Add(0.95, 3.55).Add(0.4, 3.8).Add(0, 3.85);
            Shapes.Lathe(m, SacredDraw.At(k, hu, 0, 0.9), cloth, um, lod == 0 ? 12 : 6);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                SacredParts.Pedestal(m, k, sgn * (0.5 * dw + 1.0), 0, 0.7, 0.35, 0.45, 0.6, Looks.Stone, lod);
                SacredFigures.Guardian(m, k, sgn * (0.5 * dw + 1.0), 0.6, 0.7, 1.4, GuardianKind.Lion, true, SacredFigures.Hero, lod, sgn);
            }
            return wallH + RoofH;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Basantapur tower
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A Newar palace tower (the nine-storey Basantapur tower, refs/temples/basantapur2): plain brick lower storeys
        /// with carved windows and wooden string courses, then <paramref name="roofTiers"/> real roof tiers, each a
        /// storey of carved lattice galleries under a wide tiled roof on struts, stepping in a little; the top tier a
        /// hipped roof with a gilt finial at <paramref name="totalM"/>.
        /// </summary>
        public static int PalaceTower(float w, float d, int storeys, int roofTiers, float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d;
            int tiers = Math.Max(1, Math.Min(roofTiers, storeys - 1)), lower = Math.Max(1, storeys - tiers);
            double finial = 1.0, topRoof = Math.Min(2.6, 0.26 * w);
            // Lower storeys take 45% of the height below the top roof, the tiers share the rest.
            double body = totalM - finial - topRoof, lowerH = 0.45 * body, tierH = (body - lowerH) / tiers, sH = lowerH / lower;
            int windows = 0;
            for (int s = 0; s < lower; s++)
            {
                double vs = s * sH;
                SacredParts.BandedWall(m, k, hw, hd, s == 0 ? -0.3 : vs - 0.05, vs + sH, NewarBrick, Looks.WoodDark, lod, s == 0, true);
                if (lod > 1 || s == 0) continue;
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                    double half = sd % 2 == 0 ? hd : hw, along = sd % 2 == 0 ? hw : hd;
                    int nw = s == lower - 1 ? 3 : along > 3.5 ? 2 : 1;
                    for (int q = 0; q < nw; q++)
                    {
                        double u = nw == 1 ? 0 : -0.55 * along + 1.1 * along * q / (nw - 1);
                        SacredParts.LatticeWindow(m, side, u, vs + 0.5 * sH + 0.1, half, 0.85, Math.Min(1.2, sH - 0.9), Looks.WoodDark, lod + (s == lower - 1 ? 0 : 1), 2);
                        windows++;
                    }
                }
            }
            // The roof tiers: a gallery storey and its roof, each stepping in by 0.35 m.
            double ux = hw, wx = hd, vb = lowerH;
            RoofSpec last = default;
            for (int t = 0; t < tiers; t++)
            {
                double inset = 0.35 * t;
                ux = hw - inset;
                wx = hd - inset;
                double roofRise = Math.Min(1.3, 0.38 * tierH), wallTop = vb + tierH - roofRise + 0.25;
                SacredParts.BandedWall(m, k, ux, wx, vb - 0.05, wallTop, Looks.Of(MeshColor.FromHex(0x4A2C1C), MaterialChannel.WoodCarved), Looks.WoodDark, lod, true, true);
                if (lod <= 1)
                {
                    for (int sd = 0; sd < 4; sd++)
                    {
                        Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                        double half = sd % 2 == 0 ? wx : ux, along = sd % 2 == 0 ? ux : wx;
                        int nw = Math.Max(3, (int)(2 * along / 1.1)) - (lod == 1 ? 2 : 0);
                        for (int q = 0; q < nw; q++)
                        {
                            double u = -along + 2 * along * (q + 0.5) / nw;
                            SacredParts.LatticeWindow(m, side, u, vb + 0.48 * (wallTop - vb), half, Math.Min(0.75, 1.6 * along / nw), Math.Min(1.1, wallTop - vb - 0.9), Looks.WoodDark,
                                                      lod + 1, 2);
                            windows++;
                        }
                    }
                }
                bool top = t == tiers - 1;
                double over = top ? 1.3 : 1.6;
                double apex = top ? totalM - finial : vb + tierH + 0.15;
                RoofSpec r = top
                    ? SacredParts.HipRoof(m, k, ux, wx, wallTop, over, 0.35, apex, false, 0, false, lod)
                    : SkirtRoof(m, k, ux, wx, wallTop, over, 0.35, apex, ux - 0.35, wx - 0.35, lod);
                if (lod <= 1) SacredParts.Struts(m, k, r, wallTop - 1.0, PagodaGenerator.StrutsPerSide(2 * ux, 1.3), PagodaGenerator.StrutsPerSide(2 * wx, 1.3), false, Looks.WoodDark,
                                                 (uint)(5 + t), lod);
                vb += tierH;
                last = r;
            }
            SacredParts.Gajur(m, k, totalM - finial - 0.05, finial + 0.05, Math.Max(0.2, last.TopU + 0.05), Looks.Gilt, lod);
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY, f.GroundY + totalM, hw + 1.6, hd + 1.6, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.Tiers = tiers;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A skirt roof (a tier that the next storey rises out of): a hipped tile roof over a wall of half sizes
        /// (wu, ww) from the eave up to the next storey's wall (tu, tw) at <paramref name="topV"/>.</summary>
        private static RoofSpec SkirtRoof(MeshData m, in Affine3 k, double wu, double ww, double wallTop, double overhang, double drop, double topV, double tu, double tw, int lod)
        {
            double eu = wu + overhang, ewh = ww + overhang, ev = wallTop - drop;
            double t = (eu - wu) / Math.Max(1e-6, eu - tu);
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ewh, EaveV = ev, TopU = tu - 0.03, TopW = tw - 0.03, TopV = topV, WallU = wu, WallW = ww,
                WallTopV = Math.Max(ev + 0.05, ev + t * (topV - ev) - 0.25), Lift = 0.05 * Math.Max(eu, ewh), Sag = 0.02 * Math.Min(eu - tu, ewh - tw),
                FasciaH = Math.Min(0.24, 0.1 + 0.01 * Math.Max(eu, ewh)), CoursePitch = 0.62, Gilt = false, Fringe = false, FringeH = 0.3, Horns = true,
                Roof = Looks.Of(SacredPalette.Tile, MaterialChannel.RoofTile), Ridge = Looks.Of(MeshColor.Scale(SacredPalette.Tile, 0.72f), MaterialChannel.RoofTile),
                Fascia = Looks.WoodDark, Soffit = Looks.Ao(Looks.WoodDark, 0.55f), Horn = Looks.WoodDark,
            };
            SacredParts.Roof(m, k, r, lod);
            return r;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Kal Bhairav
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Kal Bhairav (refs/temples/kalbhairav2): the open-air relief, no roof, on its platform: a grey stone niche with an
        /// arched opening, a red cornice valance and three small domes on top; inside, the red flame border and the pale
        /// blue field; the deity in black-blue: a wide stance trampling a figure, a skull garland and marigolds, six arms
        /// (sword raised, a white disc, a trident, a skull cup at the chest), a fierce red-mouthed face with white eyes
        /// under a tall gilt crown of skulls; white painted lions either side, lamps and a railing in front.
        /// </summary>
        public static int Relief(float platformW, float platformD, float reliefH, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            SacredParts.PlinthLevel(m, k, 0.5 * platformW, 0.5 * platformD, 0, 0.45, 0.3, Looks.Stone, Looks.StoneLight, lod);
            double b0 = 0.45, slabW = Math.Min(platformW - 0.6, 0.95 * reliefH), back = -0.5 * platformD + 0.55, hw = 0.5 * slabW;
            if (lod >= 2)
            {
                // The niche block, the painted field and the deity's dark mass with its gilt crown.
                SacredDraw.Box(m, k, -hw - 0.35, hw + 0.35, b0, b0 + reliefH, back - 0.5, back, Looks.Stone, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Panel(m, k, -hw, b0, hw, b0 + reliefH - 0.4, back + 0.01, Looks.Of(MeshColor.FromHex(0xC23B22), MaterialChannel.Paint));
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b0 + 0.5 * reliefH, back + 0.1), Looks.Of(MeshColor.FromHex(0x232A3D), MaterialChannel.Paint), 0.32 * reliefH, 0.42 * reliefH, 0.12, 6, false);
                Shapes.Cone(m, SacredDraw.At(k, 0, b0 + 0.85 * reliefH, back + 0.12), Looks.Gilt, 0.1 * reliefH, 0.2 * reliefH, 5, 0, 0, false);
                if (stats != null)
                {
                    stats.TopM = (float)(0.45 + reliefH);
                    stats.DoorYawDeg = f.YawDeg;
                }
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }
            double archTop = b0 + reliefH - 0.35;
            // The stone niche: a slab with an arched top, a cornice with a red valance and three small domes.
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            int n = 0, arc = lod == 0 ? 10 : 5;
            x[n] = -hw - 0.35;
            z[n++] = 0;
            x[n] = hw + 0.35;
            z[n++] = 0;
            x[n] = hw + 0.35;
            z[n++] = -(reliefH - 0.35);
            x[n] = -hw - 0.35;
            z[n++] = -(reliefH - 0.35);
            Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, b0, back - 0.5, 1, 0, 0, 0, 0, 1), Looks.Stone, x, z, n, 0.5, 0.05, 1, BevelStyle.Round, true, false);
            Shapes.RoundedBox(m, SacredDraw.At(k, 0, b0 + reliefH - 0.25, back - 0.2), Looks.StoneLight, slabW + 1.1, 0.25, 0.9, 0.04, 1);
            Shapes.RoundedBox(m, SacredDraw.At(k, 0, b0 + reliefH - 0.45, back + 0.24), Looks.Of(MeshColor.FromHex(0xC8202A), MaterialChannel.Fabric), slabW + 1.0, 0.16, 0.04, 0.02, 1);
            for (int q = -1; q <= 1; q++)
                Shapes.Dome(m, SacredDraw.At(k, q * 0.42 * slabW, b0 + reliefH - 0.12, back - 0.2), Looks.StoneLight, 0.22, 0.3, lod == 0 ? 10 : 6, true);
            // Red flame border and the pale blue field inside the arch.
            n = 0;
            x[n] = -hw;
            z[n++] = 0;
            x[n] = hw;
            z[n++] = 0;
            for (int i = 0; i <= arc; i++)
            {
                double a = Math.PI * i / arc;
                x[n] = hw * Math.Cos(a);
                z[n++] = -(archTop - b0 - hw + hw * Math.Sin(a));
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, b0, back - 0.02, 1, 0, 0, 0, 0, 1), Looks.Of(MeshColor.FromHex(0xC23B22), MaterialChannel.Paint), x, z, n, 0.08, 0.02, 1,
                                BevelStyle.Round, true, false);
            for (int i = 0; i < n; i++)
            {
                x[i] *= 0.85;
                z[i] *= 0.95;
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, b0, back + 0.04, 1, 0, 0, 0, 0, 1), Looks.Of(MeshColor.FromHex(0x8EC1DE), MaterialChannel.Paint), x, z, n, 0.03, 0, 0,
                                BevelStyle.Chamfer, true, false);
            // The deity.
            BhairavFigure(m, k, 0, b0 + 0.05, back + 0.12, reliefH - 0.45, lod);
            // Lions, lamps and the railing.
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double lu = s * (hw + 0.75);
                    SacredParts.Pedestal(m, k, lu, b0, back + 0.4, 0.3, 0.38, 0.45, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, lu, b0 + 0.45, back + 0.4, 1.55, GuardianKind.Lion, true, SacredFigures.Hero, lod, s);
                }
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x2E3440), MaterialChannel.Metal);
                double rw = 0.5 * platformD - 0.3;
                SacredDraw.Box(m, k, -0.5 * platformW + 0.3, 0.5 * platformW - 0.3, 1.25, 1.3, rw - 0.02, rw + 0.02, iron, BoxFaces.All);
                for (int q = 0; q <= 6; q++)
                {
                    double u = -0.5 * platformW + 0.3 + (platformW - 0.6) * q / 6;
                    SacredDraw.Box(m, k, u - 0.02, u + 0.02, 0.45, 1.25, rw - 0.02, rw + 0.02, iron, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                }
                for (int q = -1; q <= 1; q += 2) SacredFigures.LampPillar(m, k, q * 0.3 * platformW, 0.45, rw - 0.6, 0.9, lod);
            }
            if (c != null)
            {
                c.AddBox(kf.OX, kf.OZ, f.GroundY - 0.3, f.GroundY + 0.45, 0.5 * platformW, 0.5 * platformD, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
                double bx, by, bz;
                kf.ToWorld(0, 0, back - 0.25, out bx, out by, out bz);
                c.AddBox(bx, bz, f.GroundY + 0.45, f.GroundY + 0.45 + reliefH, hw + 0.35, 0.3, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            }
            if (stats != null)
            {
                stats.TopM = (float)(0.45 + reliefH);
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>The Kal Bhairav figure, h tall, standing against the field at (u, v, w), facing +w.</summary>
        private static void BhairavFigure(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            ShapeBrush body = Looks.Of(MeshColor.FromHex(0x232A3D), MaterialChannel.Paint), gold = Looks.Gilt, red = Looks.PaintRed;
            ShapeBrush white = Looks.Of(MeshColor.FromHex(0xF5F2EA), MaterialChannel.Paint), orange = Looks.Of(SacredPalette.Marigold, MaterialChannel.Fabric);
            ShapeBrush skin = Looks.Of(MeshColor.FromHex(0xE8B07A), MaterialChannel.Paint);
            int seg = lod == 0 ? 10 : 6, limb = lod == 0 ? 6 : 4;
            // The figure fills its arch as the real relief does (refs kalbhairav 00, 05): a broad, heavy body, so the frame
            // is widened 1.3 times across.
            Affine3 b = Affine3.Scaling(1.3, 1, 1).Then(SacredDraw.At(k, u, v, w));
            // The trampled figure lying across the base.
            Shapes.Capsule(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, 0.42 * h, 0.07 * h, 0.05)), skin, 0.06 * h, 0.84 * h, limb);
            Shapes.Sphere(m, SacredDraw.At(b, -0.46 * h, 0.08 * h, 0.06), skin, 0.065 * h, limb);
            // Wide stance: thighs out, shins down to the figure.
            for (int s = -1; s <= 1; s += 2)
            {
                Path3 p = new Path3(4);
                p.Add(s * 0.08 * h, 0.42 * h, 0.05).Add(s * 0.22 * h, 0.3 * h, 0.1).Add(s * 0.24 * h, 0.12 * h, 0.08);
                Shapes.Tube(m, b, body, p, 0.075 * h, limb, true, false, default, 0.055 * h);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.25 * h, 0.12 * h, 0.14), body, 0.06 * h, 0.035 * h, 0.08 * h, limb, false);
            }
            // Hip cloth (tiger skin), belly, chest, shoulders, head.
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.44 * h, 0.08), Looks.Of(MeshColor.FromHex(0xD98B2B), MaterialChannel.Fabric), 0.2 * h, 0.08 * h, 0.1 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.55 * h, 0.08), body, 0.17 * h, 0.12 * h, 0.1 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.68 * h, 0.06), body, 0.2 * h, 0.09 * h, 0.09 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.81 * h, 0.1), body, 0.1 * h, 0.1 * h, 0.09 * h, seg, false);
            // The face: white round eyes with pupils, a red open mouth with fangs, a third eye.
            double face = 0.1 + 0.085 * h;
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.775 * h, face), red, 0.05 * h, 0.022 * h, 0.03 * h, 6, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.042 * h, 0.83 * h, face - 0.01 * h), white, 0.026 * h, 6);
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.042 * h, 0.83 * h, face + 0.012 * h), Looks.Ink, 0.012 * h, 4);
                Shapes.Ellipsoid(m, Affine3.RotationZ(s * 0.35).Then(SacredDraw.At(b, s * 0.045 * h, 0.865 * h, face - 0.01 * h)), Looks.Of(MeshColor.FromHex(0xD9A62E), MaterialChannel.Paint), 0.035 * h, 0.008 * h, 0.015 * h, 4, false);
                if (lod == 0) Shapes.Cone(m, Affine3.RotationX(Math.PI).Then(SacredDraw.At(b, s * 0.03 * h, 0.775 * h, face + 0.02 * h)), white, 0.008 * h, 0.03 * h, 4, 0, 0, false);
            }
            if (lod == 0) Shapes.Sphere(m, SacredDraw.At(b, 0, 0.875 * h, face - 0.015 * h), white, 0.012 * h, 4);
            // The tall gilt crown with its skulls.
            Profile2 cp = Prof;
            cp.Add(0, 0).Add(0.11 * h, 0, true).Add(0.13 * h, 0.05 * h).Add(0.1 * h, 0.1 * h).Add(0.06 * h, 0.15 * h).Add(0.02 * h, 0.18 * h).Add(0, 0.18 * h);
            Shapes.Lathe(m, SacredDraw.At(b, 0, 0.89 * h, 0.08), gold, cp, seg);
            for (int q = -2; q <= 2; q++)
                Shapes.Sphere(m, SacredDraw.At(b, q * 0.05 * h, 0.93 * h - 0.01 * h * q * q, 0.08 + 0.115 * h), white, 0.022 * h, 5);
            // Garlands: skulls and marigolds across the chest.
            Path3 gl = new Path3(8);
            gl.Add(-0.18 * h, 0.72 * h, 0.12).Add(-0.08 * h, 0.55 * h, 0.2).Add(0.08 * h, 0.55 * h, 0.2).Add(0.18 * h, 0.72 * h, 0.12);
            Path3 gs = Curves.CatmullRom(gl, new Path3(16), lod == 0 ? 4 : 2, false);
            Shapes.Tube(m, b, orange, gs, 0.02 * h, limb, true);
            if (lod == 0)
            {
                // The skulls strung on the garland.
                for (int q = 1; q < gs.Count - 1; q += 2)
                    Shapes.Sphere(m, SacredDraw.At(b, gs.X[q], gs.Y[q] - 0.02 * h, gs.Z[q] + 0.02 * h), white, 0.028 * h, 5);
            }
            // Six arms: sword raised, disc, trident, skull cup.
            double[] ax = { 0.42, 0.44, 0.36 }, ay = { 0.98, 0.78, 0.58 };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int a = 0; a < 3; a++)
                {
                    Path3 p = new Path3(4);
                    p.Add(s * 0.17 * h, 0.7 * h, 0.08).Add(s * (0.17 + 0.6 * (ax[a] - 0.17)) * h, (0.7 + 0.5 * (ay[a] - 0.7)) * h - 0.03 * h, 0.1).Add(s * ax[a] * h, ay[a] * h, 0.12);
                    Shapes.Tube(m, b, body, p, 0.04 * h, limb, true, false, default, 0.03 * h);
                    Shapes.Sphere(m, SacredDraw.At(b, s * ax[a] * h, ay[a] * h, 0.12), gold, 0.025 * h, 4);
                }
            }
            // Sword (upper right): a white blade with a dark edge.
            Shapes.RoundedBox(m, Affine3.RotationZ(-0.35).Then(SacredDraw.At(b, 0.48 * h, 1.12 * h, 0.14)), white, 0.08 * h, 0.3 * h, 0.02, 0.01, 1);
            // Disc (upper left).
            Shapes.Cylinder(m, SacredDraw.Basis(b, -0.44 * h, 1.02 * h, 0.12, 1, 0, 0, 0, 0, 1), white, 0.1 * h, 0.02, seg);
            if (lod == 0) Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(b, -0.44 * h, 1.02 * h, 0.15)), Looks.Ink, 0.05 * h, 0.008 * h, 12, 3);
            // Trident (middle right) and the skull cup at the chest.
            Shapes.Cylinder(m, SacredDraw.At(b, 0.44 * h, 0.58 * h, 0.12), gold, 0.012 * h, 0.4 * h, 4);
            for (int q = -1; q <= 1; q++)
                Shapes.Cone(m, SacredDraw.At(b, 0.44 * h + q * 0.04 * h, 0.98 * h - Math.Abs(q) * 0.03 * h, 0.12), gold, 0.012 * h, 0.08 * h, 4, 0, 0, false);
            Shapes.Dome(m, Affine3.RotationX(Math.PI).Then(SacredDraw.At(b, 0, 0.62 * h, 0.24)), white, 0.06 * h, 0.04 * h, seg, true);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Courtyards
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A bahal or palace courtyard: a quadrangle of brick ranges round a walkable paved court, the gate in the middle
        /// of the front range (torana and painted lions), carved projecting windows on the outer and court faces of the
        /// upper storeys (<paramref name="windowBays"/> per side, 0 = derived; the court faces only with
        /// <paramref name="courtWindows"/>) and one continuous tiled roof over the whole ring with a red fringe on the
        /// court side (and on the street eaves too with <paramref name="outerFringe"/>), the gate's torana gilt or, without
        /// <paramref name="giltGate"/>, dark carved wood. Returns the court's inner half sizes.
        /// </summary>
        public static int Courtyard(float w, float d, float rangeDepthM, float heightM, bool lions, uint wall, in GenFrame f, int lod, MeshData m, GenColliders c,
                                    out double innerHalfU, out double innerHalfW, int windowBays = 0, bool courtWindows = true, bool outerFringe = false,
                                    bool giltGate = true)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d, r = Math.Min(rangeDepthM, 0.3 * Math.Min(w, d));
            innerHalfU = hw - r;
            innerHalfW = hd - r;
            double gate = 1.0;
            ShapeBrush wb = wall != 0 ? Looks.Of(wall, MaterialChannel.BrickGlazed) : Looks.WallGlazed;
            int storeys = lod >= 2 ? 1 : Math.Max(1, (int)Math.Round(heightM / 2.8));
            double sh = heightM / storeys;
            // The ranges as blocks (outer and court faces), the front range split by the gate passage.
            RangeWalls(m, k, -hw, -gate, hd - r, hd, storeys, sh, wb, lod);
            RangeWalls(m, k, gate, hw, hd - r, hd, storeys, sh, wb, lod);
            RangeWalls(m, k, -hw, hw, -hd, -hd + r, storeys, sh, wb, lod);
            RangeWalls(m, k, -hw, -hw + r, -hd + r, hd - r, storeys, sh, wb, lod);
            RangeWalls(m, k, hw - r, hw, -hd + r, hd - r, storeys, sh, wb, lod);
            SacredDraw.Box(m, k, -gate, gate, 2.0, heightM, hd - r, hd, wb, BoxFaces.Front | BoxFaces.Back | BoxFaces.Bottom);
            // The gate's torana: gilt (Kwa Bahal) or dark carved wood (Kumari Ghar, refs kumari 02).
            if (lod <= 1) SacredParts.Torana(m, k, 0, 2.0, hd + 0.01, 2.4, giltGate ? Looks.Gilt : Looks.WoodDark, lod);
            if (lions && lod <= 1)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * (gate + 0.7), 0, hd + 0.7, 0.32, 0.4, 0.5, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * (gate + 0.7), 0.5, hd + 0.7, 1.15, GuardianKind.Lion, true, SacredFigures.Hero, lod, sgn);
                }
            }
            // Carved windows: projecting bay windows on the front and in the court, lattice windows elsewhere.
            if (lod <= 1)
            {
                for (int s = 1; s < storeys; s++)
                {
                    double vs = s * sh;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                        double half = sd % 2 == 0 ? hd : hw, along = sd % 2 == 0 ? hw : hd;
                        int bays = windowBays > 0 ? windowBays : Math.Max(2, (int)(2 * along / 3.2));
                        for (int q = 0; q < bays; q++)
                        {
                            double u = -along + 2 * along * (q + 0.5) / bays;
                            if (sd == 0 && lod == 0) SacredParts.BayWindow(m, side, u, vs + 0.35, half, Math.Min(1.9, 1.5 * along / bays), sh - 0.7, false, lod);
                            else if (lod == 0 || sd == 0) SacredParts.LatticeWindow(m, side, u, vs + 0.5 * sh, half, 0.85, Math.Min(1.1, sh - 1.0), Looks.WoodDark, lod + 1, 2);
                        }
                        // The court faces: bay windows facing in (the top floor's centre window gilt on the far range). In the
                        // frame turned half round, range sd's court face lies at w = −inner half size and faces +w.
                        double inHalf = sd % 2 == 0 ? innerHalfW : innerHalfU, inAlong = sd % 2 == 0 ? innerHalfU : innerHalfW;
                        Affine3 inner = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI + Math.PI);
                        int ib = Math.Max(1, (int)(2 * inAlong / 3.0));
                        for (int q = 0; q < ib && lod == 0 && courtWindows && s >= storeys - 2; q++)
                        {
                            double u = -inAlong + 2 * inAlong * (q + 0.5) / ib;
                            bool gilt = sd == 0 && s == storeys - 1 && q == ib / 2;
                            SacredParts.BayWindow(m, inner, u, vs + 0.35, -inHalf, Math.Min(1.8, 1.5 * inAlong / ib), sh - 0.7, gilt, lod);
                        }
                    }
                }
            }
            // One roof over the ring: outer and court slopes meeting at a ridge, a red fringe on the court side.
            RingRoof(m, k, hw, hd, innerHalfU, innerHalfW, heightM + 0.2, heightM + 0.25 + 0.42 * r, lod, outerFringe);
            // Court paving (walkable).
            SacredDraw.CapRect(m, k, 0, 0, innerHalfU, innerHalfW, 0.05, true, Looks.Of(MeshColor.FromHex(0x9A8F84), MaterialChannel.Flagstone));
            if (c != null)
            {
                c.AddBox(kf.OX, kf.OZ, f.GroundY - 0.3, f.GroundY + 0.05, innerHalfU, innerHalfW, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
                RangeBox(c, kf, f, -hw, -gate, hd - r, hd, heightM);
                RangeBox(c, kf, f, gate, hw, hd - r, hd, heightM);
                RangeBox(c, kf, f, -hw, hw, -hd, -hd + r, heightM);
                RangeBox(c, kf, f, -hw, -hw + r, -hd + r, hd - r, heightM);
                RangeBox(c, kf, f, hw - r, hw, -hd + r, hd - r, heightM);
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        private static void RangeBox(GenColliders c, in KitFrame kf, in GenFrame f, double u0, double u1, double w0, double w1, double h)
        {
            double x, y, z;
            kf.ToWorld(0.5 * (u0 + u1), 0, 0.5 * (w0 + w1), out x, out y, out z);
            c.AddBox(x, z, f.GroundY - 0.3, f.GroundY + h, 0.5 * (u1 - u0), 0.5 * (w1 - w0), kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>The storeys of one range block from (u0, w0) to (u1, w1): banded brick walls on all its faces (no roof:
        /// the ring roof covers the quadrangle).</summary>
        private static void RangeWalls(MeshData m, in Affine3 k, double u0, double u1, double w0, double w1, int storeys, double sh, in ShapeBrush wall, int lod)
        {
            Affine3 rk = SacredDraw.At(k, 0.5 * (u0 + u1), 0, 0.5 * (w0 + w1));
            double hu = 0.5 * (u1 - u0), hw = 0.5 * (w1 - w0);
            for (int s = 0; s < storeys; s++) SacredParts.BandedWall(m, rk, hu, hw, s == 0 ? -0.3 : s * sh, (s + 1) * sh, wall, Looks.WoodDark, lod, true, s == storeys - 1);
        }

        /// <summary>One continuous roof over a quadrangle ring: the outer slope from the outer eaves and the court slope
        /// from the court eaves meet at a ridge over the middle of the ranges; red cloth fringes hang under the court
        /// eaves (Kumari Ghar).</summary>
        private static void RingRoof(MeshData m, in Affine3 k, double ou, double ow, double iu, double iw, double eaveV, double ridgeV, int lod, bool outerFringe = false)
        {
            double over = 0.7, ru = 0.5 * (ou + iu), rw = 0.5 * (ow + iw);
            uint tile = SacredPalette.Tile;
            var outer = new RoofSpec
            {
                EaveU = ou + over, EaveW = ow + over, EaveV = eaveV, TopU = ru, TopW = rw, TopV = ridgeV, WallU = ou, WallW = ow,
                WallTopV = eaveV + 0.15, Lift = 0.02 * Math.Max(ou, ow), Sag = 0.0, FasciaH = 0.2, CoursePitch = 0.9, Fringe = outerFringe && lod <= 1, FringeH = 0.34,
                Horns = true,
                Roof = Looks.Of(tile, MaterialChannel.RoofTile), Ridge = Looks.Of(MeshColor.Scale(tile, 0.72f), MaterialChannel.RoofTile), Fascia = Looks.WoodDark,
                Soffit = Looks.Ao(Looks.WoodDark, 0.55f), Horn = Looks.WoodDark,
            };
            SacredParts.Roof(m, k, outer, lod);
            var inner = outer;
            inner.EaveU = iu - over;
            inner.EaveW = iw - over;
            inner.WallU = iu;
            inner.WallW = iw;
            inner.Horns = false;
            inner.Fringe = lod <= 1;
            inner.FringeH = 0.32;
            SacredParts.Roof(m, k, inner, lod);
            // The ridge roll.
            Path3 p = new Path3(6);
            p.Add(ru, ridgeV + 0.06, rw).Add(-ru, ridgeV + 0.06, rw).Add(-ru, ridgeV + 0.06, -rw).Add(ru, ridgeV + 0.06, -rw);
            Shapes.Tube(m, k, outer.Ridge, p, 0.08, 4, false, true);
        }
    }
}
