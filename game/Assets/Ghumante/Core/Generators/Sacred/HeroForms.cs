using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>A Newar house-temple (Bhimsen, Shiva-Parvati, Kumari Ghar type; W2_DESIGN 3.2).</summary>
    public struct HouseTempleParams
    {
        public float W, D, TotalHeightM;
        public int Storeys, PlinthLevels;
        public float PlinthRiseM;
        public bool GiltBalcony, Figures, Lions;
        public uint RoofColour, WallColour;
    }

    /// <summary>The generators for the hero forms that are not pagodas, stupas or shikharas (ref_temples 5): house-temples,
    /// the open-air relief, a pillar with a kneeling figure, palace towers, the Dharahara, gates, palace fronts, bell
    /// pavilions, hitis, bahal courts and the vajra. Each builds in a <see cref="GenFrame"/> (+W through the main door)
    /// from the detailed parts kit (mouldings, carved doors and windows, real roofs with struts and bells), writes the
    /// material channels and baked AO, and returns its triangles; heights match the recipe exactly.</summary>
    public static class HeroForms
    {
        [ThreadStatic] private static Profile2 _prof;

        private static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(48))).Clear(false); }
        }

        // ---------------------------------------------------------------------------------------------------------
        // House-temple
        // ---------------------------------------------------------------------------------------------------------

        public static int HouseTemple(in HouseTempleParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * p.W, hd = 0.5 * p.D;
            int levels = Math.Max(0, p.PlinthLevels);
            double rise = p.PlinthRiseM > 0 ? p.PlinthRiseM : 0.4, plinthTop = levels * rise;
            if (lod >= 3)
            {
                double tot = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + Math.Max(1, p.Storeys) * 2.6 + 0.35 * Math.Max(p.W, p.D);
                double wt = plinthTop + 0.62 * (tot - plinthTop);
                SacredDraw.Box(m, k, -hw, hw, -0.3, wt, -hd, hd, Looks.WallGlazed, BoxFaces.All & ~BoxFaces.Bottom);
                Lod3Roof(m, k, hw + 0.8, hd + 0.8, wt, tot, Looks.Of(p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile, MaterialChannel.RoofTile));
                if (stats != null)
                {
                    stats.TopM = (float)tot;
                    stats.PlinthLevels = levels;
                    stats.DoorYawDeg = f.YawDeg;
                }
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }
            for (int i = 0; i < levels; i++)
            {
                double ins = 0.4 * i;
                SacredParts.PlinthLevel(m, k, hw + 0.6 - ins, hd + 0.6 - ins, i * rise, (i + 1) * rise, i == 0 ? 0.4 : 0.05, Looks.PlinthBrick, Looks.PlinthCoping, lod);
                if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + i * rise, f.GroundY + (i + 1) * rise, hw + 0.6 - ins, hd + 0.6 - ins, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Brick);
            }
            double run = plinthTop / Math.Tan(38 * Math.PI / 180);
            if (levels > 0) SacredParts.Stair(m, c, kf, k, 0.9, hd + 0.6 - 0.4 * (levels - 1), run + 0.4 * (levels - 1), 0, plinthTop, 0, 0, Looks.Stone, Looks.Stone, lod, GenColliders.Stone);
            int storeys = Math.Max(1, p.Storeys);
            double total = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + storeys * 2.6 + 0.35 * Math.Max(p.W, p.D);
            double finial = Math.Min(1.2, 0.08 * total);
            double roofRise = Math.Min(0.3 * Math.Max(p.W, p.D), 0.36 * (total - plinthTop - finial));
            double wallTop = total - finial - roofRise + 0.35;
            double sH = (wallTop - plinthTop) / storeys;
            ShapeBrush wall = p.WallColour != 0 ? Looks.Of(p.WallColour, MaterialChannel.BrickGlazed) : Looks.WallGlazed;
            for (int s = 0; s < storeys; s++)
                SacredParts.BandedWall(m, k, hw, hd, plinthTop + s * sH, plinthTop + (s + 1) * sH, wall, Looks.WoodDark, lod, true, s == storeys - 1);
            SacredDraw.CapRect(m, k, 0, 0, hw, hd, wallTop, true, wall);
            // Ground floor: the closed sanctum door and two side doors; upper floors: carved windows.
            double dh = Math.Min(2.0, sH - 0.4);
            SacredParts.Door(m, k, 0, plinthTop, hd, 1.0, dh, Looks.WoodDark, lod, true, plinthTop + sH - 0.2);
            int windows = 0;
            for (int sd = 0; sd < 4; sd++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                double half = sd % 2 == 0 ? hd : hw, along = sd % 2 == 0 ? hw : hd;
                int bays = Math.Max(1, Math.Min(5, (int)Math.Round(2 * along / 1.9)));
                if (bays % 2 == 0) bays++;
                for (int s = 0; s < storeys; s++)
                {
                    double vs = plinthTop + s * sH;
                    for (int b = 0; b < bays; b++)
                    {
                        double u = bays == 1 ? 0 : -along + 2 * along * (b + 0.5) / bays;
                        bool centre = b == bays / 2;
                        if (s == 0)
                        {
                            if (sd == 0 && centre) continue;
                            if (lod <= 1 && (sd == 0 || b % 2 == 1)) SacredParts.LatticeWindow(m, side, u, vs + 0.55 * sH, half, 0.7, 0.8, Looks.WoodDark, lod + (sd == 0 ? 0 : 1));
                            continue;
                        }
                        double ww = centre && sd == 0 ? 1.3 : 0.85, wh = Math.Min(1.25, sH - 0.8);
                        if (centre && sd == 0 && p.Figures && s == storeys - 1)
                        {
                            // Shiva and Parvati looking out of the open central window.
                            SacredDraw.Panel(m, side, u - 0.5 * ww, vs + 0.45, u + 0.5 * ww, vs + 0.45 + wh, half + 0.012, Looks.Dark);
                            SacredParts.LatticeWindow(m, side, u, vs + 0.45 + 0.5 * wh, half, ww, wh, Looks.WoodDark, 1);
                            if (lod <= 1) Figures(m, side, u, vs + 0.45, half + 0.12, lod);
                        }
                        else
                        {
                            SacredParts.LatticeWindow(m, side, u, vs + 0.4 + 0.5 * wh, half, ww, wh, Looks.WoodDark, lod + (sd == 0 ? 0 : 1));
                        }
                        windows++;
                    }
                }
            }
            if (p.GiltBalcony && lod <= 1)
            {
                // Bhimsen's gilt balcony across the top floor of the front.
                double vb = plinthTop + (storeys - 1) * sH;
                Mould b = SacredDraw.M;
                b.Add(0, vb - 0.2, Looks.Gilt).Add(0.75, vb + 0.05).Add(0.75, vb + 1.15, Looks.GiltAged).Add(0.68, vb + 1.25, Looks.Gilt).Add(0, vb + 1.25);
                double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
                int n = SacredDraw.Rect(0, hd - 0.4, 0.8 * hw, 0.4, pu, pw);
                SacredDraw.Ring(m, k, pu, pw, n, b);
                for (int q = 0; q < 6; q++)
                {
                    double u = -0.7 * hw + 1.4 * hw * (q + 0.5) / 6;
                    SacredDraw.Panel(m, k, u - 0.08 * hw, vb + 0.25, u + 0.08 * hw, vb + 1.0, hd + 0.36, Looks.GiltHi);
                }
                SacredParts.HipRoof(m, SacredDraw.At(k, 0, 0, hd + 0.1), 0.8 * hw, 0.4, vb + 1.85, 0.35, 0.15, vb + 2.15, true, 0, false, lod + 1);
            }
            // Hipped tile roof with struts and bells, gilt finials along the ridge.
            uint tile = p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile;
            double apex = total - finial;
            RoofSpec r = SacredParts.HipRoof(m, k, hw, hd, wallTop, Math.Min(1.4, 0.12 * Math.Max(p.W, p.D) + 0.4), 0.35, apex, false, tile, false, lod);
            if (lod <= 1)
            {
                SacredParts.Struts(m, k, r, wallTop - 1.3, PagodaGenerator.StrutsPerSide(2 * hw), PagodaGenerator.StrutsPerSide(2 * hd), true, Looks.WoodDark, 31u, lod);
                SacredParts.Bells(m, k, r, 0.5, 1.0, false, lod);
            }
            int fin = r.TopU > 0.2 ? 3 : 1;
            for (int q = 0; q < fin; q++)
            {
                double u = fin == 1 ? 0 : (q - 1) * 0.8 * r.TopU;
                SacredParts.Gajur(m, SacredDraw.At(k, u, 0, 0), apex - 0.05, finial + 0.05, 0.2, Looks.Gilt, lod);
            }
            if (p.Lions && lod <= 2)
            {
                double gw = hd + 0.6 + run - 0.2;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * 1.35, 0, gw, 0.3, 0.38, 0.5, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * 1.35, 0.5, gw, 1.2, GuardianKind.Lion, true, true, lod);
                }
            }
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + plinthTop, f.GroundY + total, hw, hd, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = (float)total;
                stats.PlinthLevels = levels;
                stats.PlinthTopM = (float)plinthTop;
                stats.DoorYawDeg = f.YawDeg;
                stats.Windows = windows;
                stats.SanctumHalfU = (float)hw;
                stats.SanctumHalfW = (float)hd;
                stats.SanctumV0 = (float)plinthTop;
                stats.SanctumV1 = (float)(plinthTop + sH);
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A four-sided pyramid roof (LOD3).</summary>
        private static void Lod3Roof(MeshData m, in Affine3 k, double hu, double hw, double v0, double v1, in ShapeBrush b)
        {
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, ou, ow;
                SacredParts.OnRect(s, -1, hu, hw, out au, out aw);
                SacredParts.OnRect(s, 1, hu, hw, out bu, out bw);
                SacredParts.Out(s, out ou, out ow);
                SacredDraw.Tri(m, k, au, v0, aw, bu, v0, bw, 0, v1, 0, ou, 1, ow, b);
            }
        }

        /// <summary>The white painted Shiva and Parvati leaning out of the central window, side by side.</summary>
        private static void Figures(MeshData m, in Affine3 k, double u, double v, double w, int lod)
        {
            ShapeBrush white = Looks.Of(MeshColor.FromHex(0xF4F1E8), MaterialChannel.Paint);
            int s = lod == 0 ? 6 : 4;
            for (int q = -1; q <= 1; q += 2)
            {
                double x = u + q * 0.24;
                Shapes.Ellipsoid(m, SacredDraw.At(k, x, v + 0.45, w), white, 0.17, 0.26, 0.12, s, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, x, v + 0.85, w + 0.02), white, 0.11, 0.13, 0.11, s, false);
                Shapes.Cone(m, SacredDraw.At(k, x, v + 0.95, w + 0.02), Looks.Gilt, 0.08, 0.16, 5, 0, 0, false);
                Shapes.Capsule(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(k, x - q * 0.1, v + 0.3, w - 0.05)), white, 0.05, 0.4, 4);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Kal Bhairav relief, Yoganarendra column, vajra
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An open-air stone relief on a platform, no roof (Kal Bhairav: black-blue body, six arms, gilt crown,
        /// a fierce face with white eyes; a low railing and lamps in front).</summary>
        public static int Relief(float platformW, float platformD, float reliefH, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            SacredParts.PlinthLevel(m, k, 0.5 * platformW, 0.5 * platformD, 0, 0.45, 0.3, Looks.Stone, Looks.StoneLight, lod);
            double slabW = Math.Min(platformW - 0.6, 0.75 * reliefH), back = -0.5 * platformD + 0.5;
            // Back slab with an arched crown.
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            int n = 0;
            x[n] = -0.5 * slabW;
            z[n++] = 0;
            x[n] = 0.5 * slabW;
            z[n++] = 0;
            int arc = lod == 0 ? 8 : 4;
            for (int i = 0; i <= arc; i++)
            {
                double a = Math.PI * i / arc;
                x[n] = 0.5 * slabW * Math.Cos(a);
                z[n++] = -(0.8 * reliefH + 0.2 * reliefH * Math.Sin(a));
            }
            Affine3 sb = SacredDraw.Basis(k, 0, 0.45, back - 0.45, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, sb, Looks.Stone, x, z, n, 0.45, 0.06, 1, BevelStyle.Round, true, false);
            ShapeBrush body = Looks.Of(MeshColor.FromHex(0x2A3550), MaterialChannel.Paint), gold = Looks.Gilt;
            ShapeBrush yellow = Looks.Of(MeshColor.FromHex(0xF2C230), MaterialChannel.Paint);
            int s = lod == 0 ? 8 : 4;
            double bw = 0.5 * slabW, b0 = 0.45, fw = back + 0.2;
            // Legs, torso with a yellow skirt, head with crown, six arms.
            Shapes.Capsule(m, SacredDraw.At(k, -0.14 * slabW, b0, fw), body, 0.1 * bw + 0.05, 0.42 * reliefH, s >= 8 ? 6 : 4);
            Shapes.Capsule(m, SacredDraw.At(k, 0.14 * slabW, b0, fw), body, 0.1 * bw + 0.05, 0.42 * reliefH, s >= 8 ? 6 : 4);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b0 + 0.42 * reliefH, fw + 0.05), yellow, 0.34 * bw + 0.1, 0.1 * reliefH, 0.25, s, s >= 8);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b0 + 0.56 * reliefH, fw + 0.02), body, 0.3 * bw + 0.08, 0.17 * reliefH, 0.24, s, s >= 8);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b0 + 0.8 * reliefH, fw + 0.08), body, 0.18 * bw + 0.06, 0.1 * reliefH, 0.24, s, s >= 8);
            Shapes.Frustum(m, SacredDraw.At(k, 0, b0 + 0.87 * reliefH, fw + 0.06), gold, 0.18 * bw + 0.08, 0.1 * bw + 0.04, 0.13 * reliefH, s >= 8 ? 8 : 5, 0, 0, false);
            if (lod <= 1)
            {
                // Fierce face: white round eyes, red tongue.
                for (int q = -1; q <= 1; q += 2)
                    Shapes.Ellipsoid(m, SacredDraw.At(k, q * 0.06 * slabW, b0 + 0.82 * reliefH, fw + 0.28), Looks.Of(0xFFFFFFFFu, MaterialChannel.Paint), 0.05 * slabW, 0.05 * slabW, 0.03, 4, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b0 + 0.76 * reliefH, fw + 0.28), Looks.PaintRed, 0.05 * slabW, 0.02 * reliefH, 0.03, 4, false);
                for (int a = 0; a < 6; a++)
                {
                    double sideA = a < 3 ? -1 : 1, lvl = b0 + reliefH * (0.5 + 0.1 * (a % 3));
                    double ax = sideA * 0.24 * slabW, bx = sideA * 0.45 * slabW, by = lvl + 0.12 * reliefH * (a % 3 - 1);
                    double len;
                    Affine3 arm = Affine3.Along(ax, lvl, fw + 0.1, bx, by, fw + 0.1, out len).Then(k);
                    Shapes.Capsule(m, Affine3.Translation(0, -0.06, 0).Then(arm), body, 0.06, len + 0.12, 4);
                    Shapes.Sphere(m, SacredDraw.At(k, bx, by, fw + 0.1), a % 3 == 1 ? gold : body, 0.08, 5);
                }
                // A low iron railing and two lamps in front.
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
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY - 0.3, f.GroundY + 0.45, 0.5 * platformW, 0.5 * platformD, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            if (stats != null)
            {
                stats.TopM = (float)(0.45 + reliefH);
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A stone pillar with a lotus capital and a kneeling gilt king under a cobra hood (Yoganarendra
        /// Malla), or with a Garuda when <paramref name="garuda"/>.</summary>
        public static int Column(float heightM, bool garuda, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            SacredParts.PlinthLevel(m, k, 1.0, 1.0, 0, 0.4, 0.3, Looks.Stone, Looks.StoneLight, lod);
            SacredParts.PlinthLevel(m, k, 0.65, 0.65, 0.4, 0.8, 0.05, Looks.Stone, Looks.StoneLight, lod);
            double figH = 1.45, shaft = Math.Max(1.0, heightM - 0.8 - figH);
            Profile2 p = Prof;
            double y0 = 0.8, y1 = 0.8 + shaft;
            p.Add(0, y0).Add(0.42, y0, true).Add(0.42, y0 + 0.15, true).Add(0.3, y0 + 0.25).Add(0.3, y0 + 0.35, true).Add(0.27, y0 + 0.4)
             .Add(0.25, y1 - 0.5).Add(0.3, y1 - 0.42, true).Add(0.24, y1 - 0.35).Add(0.32, y1 - 0.15).Add(0.55, y1, true).Add(0, y1);
            Shapes.Lathe(m, k, Looks.Stone, p, SacredParts.Seg(12, lod));
            ShapeBrush fig = garuda ? Looks.Stone : Looks.Gilt;
            if (garuda)
            {
                SacredFigures.Garuda(m, k, 0, y1, 0, figH, Looks.Stone, lod);
            }
            else
            {
                // The kneeling king with folded hands, his crown and the cobra hood behind.
                int s = lod == 0 ? 8 : 4;
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, y1 + 0.25, 0), fig, 0.32, 0.22, 0.3, s, s >= 8);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, y1 + 0.62, 0.02), fig, 0.24, 0.3, 0.18, s, s >= 8);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, y1 + 1.02, 0.04), fig, 0.15, 0.16, 0.15, s, s >= 8);
                Shapes.Cone(m, SacredDraw.At(k, 0, y1 + 1.12, 0.04), Looks.GiltHi, 0.14, 0.3, 6, 0, 0, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, y1 + 0.66, 0.2), fig, 0.08, 0.12, 0.06, 4, false);
                if (lod <= 1)
                {
                    double[] hx = ShapeScratch.U, hz = ShapeScratch.W;
                    int hn = 0, heads = 7;
                    hx[hn] = -0.45;
                    hz[hn++] = 0;
                    hx[hn] = 0.45;
                    hz[hn++] = 0;
                    for (int i = 0; i <= heads; i++)
                    {
                        double a = Math.PI * i / heads, rr = i % 2 == 0 ? 0.62 : 0.52;
                        hx[hn] = rr * Math.Cos(a);
                        hz[hn++] = -(0.6 + rr * Math.Sin(a));
                    }
                    Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, y1 + 0.55, -0.22, 1, 0, 0, 0, 0, 1), Looks.Gilt, hx, hz, hn, 0.08, 0.03, 1, BevelStyle.Round, true, false);
                }
            }
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + heightM, 1.0, 1.0, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            if (stats != null)
            {
                stats.TopM = (float)(y1 + figH);
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>Swayambhu's great gilt vajra (about 3 m long) on its drum pedestal (a mandala drum with a band of
        /// animal reliefs).</summary>
        public static int Vajra(in GenFrame f, MeshData m)
        {
            return Vajra(f, 0, m);
        }

        /// <summary>The vajra at a LOD (LOD2 and beyond: a drum and a bar).</summary>
        public static int Vajra(in GenFrame f, int lod, MeshData m)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            if (lod >= 2)
            {
                Shapes.Cylinder(m, SacredDraw.At(k, 0, -0.2, 0), Looks.Stone, 1.45, 1.3, 8, 0, 0, false, true);
                Shapes.Capsule(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(k, 1.4, 1.6, 0)), Looks.Gilt, 0.3, 2.8, 4);
                return m.TriangleCount - t0;
            }
            Profile2 p = Prof;
            p.Add(0, -0.2).Add(1.5, -0.2, true).Add(1.5, 0.15, true).Add(1.35, 0.2).Add(1.35, 0.85).Add(1.45, 0.95, true).Add(1.45, 1.1, true).Add(0, 1.1);
            Shapes.Lathe(m, k, Looks.Stone, p, 20);
            Shapes.Sphere(m, SacredDraw.At(k, 0, 1.6, 0), Looks.GiltHi, 0.32, 10);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Profile2 b = Prof;
                b.Add(0, 0).Add(0.24, 0.05).Add(0.38, 0.3).Add(0.3, 0.55).Add(0.12, 0.75).Add(0, 0.8);
                Affine3 x = Affine3.RotationZ(sgn * 0.5 * Math.PI).Then(SacredDraw.At(k, -sgn * 0.3, 1.6, 0));
                Shapes.Lathe(m, x, Looks.Gilt, b, 10);
                for (int q = 0; q < 4; q++)
                {
                    double a = 0.5 * Math.PI * q + 0.25 * Math.PI, cy = Math.Cos(a) * 0.32, cz = Math.Sin(a) * 0.32;
                    Path3 t = new Path3(4);
                    t.Add(sgn * 0.75, 1.6 + cy, cz).Add(sgn * 1.1, 1.6 + 1.2 * cy, 1.2 * cz).Add(sgn * 1.45, 1.6 + 0.4 * cy, 0.4 * cz);
                    Shapes.Tube(m, k, Looks.Gilt, t, 0.06, 5, true, false, default, 0.03);
                }
                Shapes.Cone(m, Affine3.RotationZ(-sgn * 0.5 * Math.PI).Then(SacredDraw.At(k, sgn * 1.25, 1.6, 0)), Looks.Gilt, 0.08, 0.35, 6, 0, 0, false);
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Towers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A Newar palace tower (the nine-storey Basantapur tower): brick storeys with carved windows and wooden
        /// string courses, tiered sloped roofs with struts on the upper storeys and a top roof with a gilt finial.</summary>
        public static int PalaceTower(float w, float d, int storeys, int roofTiers, float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double topRoof = 0.3 * w, finial = 1.0, sH = (totalM - topRoof - finial) / storeys;
            double hw = 0.5 * w, hd = 0.5 * d;
            int windows = 0, first = storeys - roofTiers;
            for (int s = 0; s < storeys; s++)
            {
                double vs = s * sH, shrink = s > first ? 1.0 - 0.07 * (s - first) : 1.0;
                double ux = hw * shrink, wx = hd * shrink;
                SacredParts.BandedWall(m, k, ux, wx, s == 0 ? -0.3 : vs - 0.05, vs + sH, Looks.WallGlazed, Looks.WoodDark, lod, s == 0, true);
                if (s > first && lod <= 2)
                {
                    // A skirt roof round the foot of each upper storey.
                    double pu = hw * (1.0 - 0.07 * (s - 1 - first)), pw = hd * (1.0 - 0.07 * (s - 1 - first));
                    SacredParts.HipRoof(m, k, pu, pw, vs + 0.1, 0.9, 0.55, vs + 0.6, false, 0, false, lod + 1);
                }
                if (lod <= 1 && s > 0)
                {
                    for (int sd = 0; sd < 4; sd++)
                    {
                        Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                        double half = sd % 2 == 0 ? wx : ux, along = sd % 2 == 0 ? ux : wx;
                        int nw = along > 3.5 ? 3 : 1;
                        for (int q = 0; q < nw; q++)
                        {
                            double u = nw == 1 ? 0 : -along + 2 * along * (q + 0.5) / nw;
                            SacredParts.LatticeWindow(m, side, u, vs + 0.5 * sH + 0.1, half, 0.8, Math.Min(1.3, sH - 0.9), Looks.WoodDark, lod + 1, 2);
                            windows++;
                        }
                    }
                }
            }
            double tw = hw * (1.0 - 0.07 * (storeys - 1 - first)), td = hd * (1.0 - 0.07 * (storeys - 1 - first));
            double apex = totalM - finial;
            RoofSpec r = SacredParts.HipRoof(m, k, tw, td, storeys * sH, 1.1, 0.3, apex, false, 0, false, lod);
            if (lod <= 1) SacredParts.Struts(m, k, r, storeys * sH - 1.4, 3, 3, false, Looks.WoodDark, 5u, lod);
            SacredParts.Gajur(m, k, apex - 0.05, finial + 0.05, 0.22, Looks.Gilt, lod);
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY, f.GroundY + totalM, hw, hd, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.Tiers = roofTiers;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>The 2021 Dharahara (rebuilt "in the old style with a larger diameter"): a moulded podium, a white
        /// fluted shaft of 22 storeys tapering to the top drum, storey bands and window slots, the balcony gallery with its
        /// railing, the cupola and the bronze mast, to exactly <paramref name="totalM"/>.</summary>
        public static int Dharahara(float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            double s = totalM / 72.0;
            double baseTop = 1.4 * s, shaftTop = 57.0 * s, galleryTop = 58.4 * s, drumTop = 63.0 * s, capTop = 67.0 * s;
            int sides = lod == 0 ? 32 : lod == 1 ? 20 : 10;
            ShapeBrush white = Looks.Of(MeshColor.FromHex(0xF7F5EF), MaterialChannel.Plaster), trim = Looks.Of(MeshColor.FromHex(0xE6E1D6), MaterialChannel.Plaster);
            Profile2 pb = Prof;
            pb.Add(0, -0.3).Add(7.6, -0.3, true).Add(7.6, 0.4 * baseTop, true).Add(7.2, 0.5 * baseTop, true).Add(7.2, baseTop, true).Add(0, baseTop);
            Shapes.Lathe(m, k, trim, pb, sides);
            // Fluted shaft.
            int rows = lod == 0 ? 23 : lod == 1 ? 8 : 2, n = sides;
            int fst = m.VertexCount;
            for (int r = 0; r <= rows; r++)
            {
                double t = (double)r / rows, y = baseTop + (shaftTop - baseTop) * t, rad = 5.4 * (1 - 0.16 * t);
                bool band = lod == 0 && r > 0 && r < rows;
                for (int i = 0; i <= n; i++)
                {
                    double a = 2 * Math.PI * i / n, flute = i % 2 == 0 ? 1.0 : 0.93, rr = rad * flute * (band ? 1.02 : 1.0);
                    SacredDraw.V(m, k, rr * Math.Sin(a), y, rr * Math.Cos(a), Math.Sin(a), 0.05, Math.Cos(a), band ? trim : white);
                }
            }
            SacredDraw.Grid(m, fst, rows + 1, n + 1, false, null);
            if (lod <= 1)
            {
                int storeys = 22, slots = 0;
                double sh = (shaftTop - baseTop) / storeys;
                for (int q = 1; q < storeys; q += lod == 0 ? 1 : 3)
                {
                    double t = (q * sh) / (shaftTop - baseTop), rr = 5.4 * (1 - 0.16 * t);
                    for (int sd = 0; sd < 4; sd++)
                    {
                        Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI + 0.25 * Math.PI * (q % 2));
                        SacredDraw.Panel(m, side, -0.22, baseTop + q * sh + 0.35, 0.22, baseTop + q * sh + 0.35 + 0.55 * sh, rr + 0.02, Looks.Of(MeshColor.FromHex(0x3A4A5C), MaterialChannel.Glass));
                        slots++;
                    }
                }
                if (stats != null) stats.Windows = slots;
            }
            // Gallery slab, railing posts, top drum, cupola, mast.
            Profile2 pg = Prof;
            pg.Add(0, shaftTop - 0.6).Add(4.6, shaftTop - 0.6, true).Add(6.4, shaftTop - 0.05, true).Add(6.4, shaftTop + 0.25, true).Add(0, shaftTop + 0.25);
            Shapes.Lathe(m, k, trim, pg, sides);
            if (lod <= 1)
            {
                Profile2 rail = Prof;
                rail.Add(6.25, galleryTop - 0.02).Add(6.35, galleryTop + 0.02, true).Add(6.35, galleryTop + 0.1, true).Add(6.25, galleryTop + 0.1);
                Shapes.Lathe(m, k, Looks.Of(MeshColor.FromHex(0xC9CED6), MaterialChannel.Metal), rail, sides);
                int posts = lod == 0 ? 24 : 12;
                for (int q = 0; q < posts; q++)
                {
                    double a = 2 * Math.PI * q / posts;
                    Shapes.Cylinder(m, SacredDraw.At(k, 6.3 * Math.Sin(a), shaftTop + 0.25, 6.3 * Math.Cos(a)), Looks.Of(MeshColor.FromHex(0xC9CED6), MaterialChannel.Metal), 0.04,
                                    galleryTop + 0.05 - shaftTop - 0.25, 4, 0, 0, false, false);
                }
            }
            Profile2 pd = Prof;
            pd.Add(0, shaftTop + 0.2).Add(4.7, shaftTop + 0.2, true).Add(4.7, drumTop - 0.6).Add(5.1, drumTop - 0.3, true).Add(5.1, drumTop, true).Add(4.6, drumTop, true);
            int cap = lod == 0 ? 8 : 4;
            for (int q = 1; q <= cap; q++)
            {
                double a = 0.5 * Math.PI * q / cap;
                pd.Add(4.6 * Math.Cos(a) + 0.25 * (q == cap ? 1 : 0), drumTop + (capTop - drumTop) * Math.Sin(a), q == cap);
            }
            pd.Add(0, capTop);
            Shapes.Lathe(m, k, white, pd, sides);
            if (lod <= 1)
            {
                for (int sd = 0; sd < 8; sd++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.25 * Math.PI);
                    SacredDraw.Panel(m, side, -0.45, shaftTop + 1.0, 0.45, drumTop - 1.0, 4.72, Looks.Of(MeshColor.FromHex(0x3A4A5C), MaterialChannel.Glass));
                }
            }
            Profile2 pm = Prof;
            pm.Add(0, capTop - 0.1).Add(0.45, capTop - 0.1, true).Add(0.35, capTop + 0.4).Add(0.5, capTop + 0.9).Add(0.2, capTop + 1.3).Add(0.12, totalM - 1.5)
              .Add(0.22, totalM - 1.2).Add(0.06, totalM - 0.5).Add(0, totalM);
            Shapes.Lathe(m, k, Looks.Of(MeshColor.FromHex(0x8C6A3A), MaterialChannel.Metal), pm, lod == 0 ? 10 : 6);
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + totalM, 7.6, 7.6, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gates and palace fronts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A palace gate: <paramref name="golden"/> = Bhaktapur's Golden Gate (gilt torana and door in a bright red
        /// gatehouse set in white walls under a small tile roof); else Hanuman Dhoka (a gilt door in the painted palace
        /// front, the Hanuman statue wrapped in red cloth under a red parasol, two painted lions).</summary>
        public static int Gate(bool golden, float widthM, float heightM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * widthM + 1.0, depth = 2.5;
            const double RoofH = 1.4;
            double wallH = Math.Max(3.0, heightM - RoofH);
            ShapeBrush house = golden ? Looks.Of(MeshColor.FromHex(0xB32621), MaterialChannel.Paint) : Looks.WallGlazed;
            SacredParts.BandedWall(m, SacredDraw.At(k, 0, 0, -0.5 * depth), hw, 0.5 * depth, -0.3, wallH, house, Looks.WoodDark, lod, true, true);
            double dw = Math.Min(widthM - 0.6, 2.4), dh = Math.Min(wallH - 1.6, 3.2);
            SacredParts.Door(m, k, 0, 0, 0, dw, dh, Looks.Gilt, lod, true, wallH - 0.1);
            if (!golden) SacredDraw.Panel(m, k, -0.5 * dw, 0, 0.5 * dw, dh, 0.013, Looks.Of(SacredPalette.GiltLo, MaterialChannel.Gilt));
            SacredParts.HipRoof(m, SacredDraw.At(k, 0, 0, -0.5 * depth), hw, 0.5 * depth, wallH, 0.7, 0.2, wallH + RoofH, false, 0, false, lod);
            if (golden)
            {
                ShapeBrush lime = Looks.Of(MeshColor.FromHex(0xF4F2EC), MaterialChannel.Plaster);
                SacredParts.BandedWall(m, SacredDraw.At(k, -hw - 7, 0, -0.5 * depth), 7, 0.5 * depth - 0.3, -0.3, wallH - 1.0, lime, Looks.WoodDark, lod, false, true);
                SacredParts.BandedWall(m, SacredDraw.At(k, hw + 7, 0, -0.5 * depth), 7, 0.5 * depth - 0.3, -0.3, wallH - 1.0, lime, Looks.WoodDark, lod, false, true);
            }
            else
            {
                // Hanuman to the left of the gate, wrapped in red under a red parasol; lions either side.
                double hu = -hw - 1.6;
                SacredParts.Pedestal(m, k, hu, 0, 1.2, 0.7, 0.7, 1.0, Looks.Stone, lod);
                ShapeBrush cloth = Looks.Of(MeshColor.FromHex(0xC8202A), MaterialChannel.Fabric);
                Shapes.Ellipsoid(m, SacredDraw.At(k, hu, 1.8, 1.2), cloth, 0.5, 0.85, 0.42, lod == 0 ? 8 : 4, lod == 0);
                Shapes.Ellipsoid(m, SacredDraw.At(k, hu, 2.75, 1.25), Looks.Of(MeshColor.FromHex(0xE05A2B), MaterialChannel.Paint), 0.25, 0.28, 0.25, lod == 0 ? 8 : 4, lod == 0);
                Shapes.Cylinder(m, SacredDraw.At(k, hu, 1.0, 0.9), Looks.Of(MeshColor.FromHex(0x5A5A5A), MaterialChannel.Metal), 0.04, 2.8, 4);
                Profile2 um = Prof;
                um.Add(0, 3.4).Add(1.05, 3.4, true).Add(0.95, 3.55).Add(0.4, 3.8).Add(0, 3.85);
                Shapes.Lathe(m, SacredDraw.At(k, hu, 0, 0.9), cloth, um, lod == 0 ? 12 : 6);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * (0.5 * dw + 1.0), 0, 0.7, 0.35, 0.45, 0.6, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * (0.5 * dw + 1.0), 0.6, 0.7, 1.4, GuardianKind.Lion, true, true, lod);
                }
            }
            if (c != null)
            {
                double x, y, z;
                kf.ToWorld(-0.5 * hw - 0.5 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + wallH, 0.5 * (hw - 0.5 * dw), 0.5 * depth, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
                kf.ToWorld(0.5 * hw + 0.5 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + wallH, 0.5 * (hw - 0.5 * dw), 0.5 * depth, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
            if (stats != null)
            {
                stats.TopM = (float)(wallH + RoofH);
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A palace block: <paramref name="rana"/> = a neoclassical Rana front (white stucco, tall French windows
        /// with green shutters, cornices, a pediment and a balustrade; Gaddi Baithak); else a Newar palace range with a row
        /// of carved windows on the upper floor (<paramref name="upperWindows"/>: 55 for the 55-Window Palace) under a tile
        /// roof with struts.</summary>
        public static int Palace(bool rana, float w, float d, float h, int storeys, int upperWindows, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d;
            double roofRise = rana ? 1.0 : 0.25 * d, wallTop = h - roofRise, sH = wallTop / storeys;
            int windows = 0;
            if (rana)
            {
                ShapeBrush stucco = Looks.Of(MeshColor.FromHex(0xF8F5EC), MaterialChannel.Plaster), white = Looks.Of(0xFFFFFFFFu, MaterialChannel.Plaster);
                ShapeBrush shutter = Looks.Of(MeshColor.FromHex(0x4E7D52), MaterialChannel.Paint);
                for (int s = 0; s < storeys; s++)
                    SacredParts.BandedWall(m, k, hw, hd, s == 0 ? -0.4 : s * sH, (s + 1) * sH, stucco, white, lod, true, true);
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                    double half = sd % 2 == 0 ? hd : hw, along = sd % 2 == 0 ? hw : hd;
                    int bays = Math.Max(2, (int)Math.Round(2 * along / 3.3));
                    for (int s = 0; s < storeys; s++)
                    {
                        for (int b = 0; b < bays; b++)
                        {
                            double u = -along + 2 * along * (b + 0.5) / bays, wt = s * sH + Math.Min(sH - 0.7, 3.1);
                            SacredDraw.Box(m, side, u - 0.78, u + 0.78, s * sH + 0.55, wt + 0.1, half, half + 0.1, white, lod <= 1 ? BoxFaces.Wall : BoxFaces.Front);
                            SacredDraw.Panel(m, side, u - 0.6, s * sH + 0.65, u + 0.6, wt, half + 0.105, Looks.Of(MeshColor.FromHex(0x34404A), MaterialChannel.Glass));
                            if (lod == 0 || lod == 1 && sd == 0)
                            {
                                SacredDraw.Panel(m, side, u - 0.9, s * sH + 0.65, u - 0.62, wt, half + 0.12, shutter);
                                SacredDraw.Panel(m, side, u + 0.62, s * sH + 0.65, u + 0.9, wt, half + 0.12, shutter);
                                SacredDraw.Box(m, side, u - 0.95, u + 0.95, wt + 0.1, wt + 0.3, half, half + 0.25, white, BoxFaces.Wall | BoxFaces.Bottom);
                            }
                            if (sd == 0) windows++;
                        }
                    }
                }
                // Cornice, pediment over the central bays and the balustrade.
                double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
                int n = SacredDraw.Rect(0, 0, hw, hd, pu, pw);
                Mould cm = SacredDraw.M;
                cm.Add(0, wallTop - 0.5, white).Add(0.15, wallTop - 0.4).Add(0.15, wallTop - 0.2).Add(0.45, wallTop - 0.05).Add(0.45, wallTop);
                SacredDraw.Ring(m, k, pu, pw, n, cm);
                SacredDraw.Cap(m, k, pu, pw, n, 0.45, wallTop, true, stucco);
                double[] tx = ShapeScratch.U, tz = ShapeScratch.W;
                tx[0] = -0.2 * w;
                tz[0] = 0;
                tx[1] = 0.2 * w;
                tz[1] = 0;
                tx[2] = 0;
                tz[2] = -0.12 * w;
                Shapes.BevelExtrude(m, SacredDraw.Basis(k, 0, wallTop, hd + 0.05, 1, 0, 0, 0, 0, 1), white, tx, tz, 3, 0.4, 0.05, 1, BevelStyle.Chamfer, true, false);
                Mould bm = SacredDraw.M;
                bm.Add(0.3, wallTop, Looks.Of(MeshColor.FromHex(0xF1E3BE), MaterialChannel.Plaster)).Add(0.3, wallTop + 0.8).Add(0.42, wallTop + 0.85).Add(0.42, h).Add(0.1, h).Add(0.1, wallTop);
                SacredDraw.Ring(m, k, pu, pw, n, bm);
            }
            else
            {
                for (int s = 0; s < storeys; s++)
                    SacredParts.BandedWall(m, k, hw, hd, s == 0 ? -0.4 : s * sH, (s + 1) * sH, Looks.WallGlazed, Looks.WoodDark, lod, true, s == storeys - 1);
                for (int s = 0; s < storeys; s++)
                {
                    int count = s == storeys - 1 && upperWindows > 0 ? upperWindows : Math.Max(1, (int)Math.Round(w / 2.5));
                    double pitch = w / count;
                    for (int b = 0; b < count; b++)
                    {
                        double u = -hw + pitch * (b + 0.5), ww = Math.Min(0.72, 0.78 * pitch), wh = Math.Min(sH - 0.6, 1.3);
                        if (lod >= 2)
                        {
                            if (s == storeys - 1) SacredDraw.Panel(m, k, u - 0.5 * ww, s * sH + 0.45, u + 0.5 * ww, s * sH + 0.45 + wh, hd + 0.01, Looks.Recess);
                        }
                        else
                        {
                            SacredParts.LatticeWindow(m, k, u, s * sH + 0.45 + 0.5 * wh, hd, ww, wh, Looks.WoodDark, lod + (s == storeys - 1 ? 1 : 2), 2);
                        }
                        if (s == storeys - 1) windows++;
                    }
                }
                RoofSpec r = SacredParts.HipRoof(m, k, hw, hd, wallTop + 0.3, 1.0, 0.3, h, false, 0, false, lod);
                if (lod <= 1) SacredParts.Struts(m, k, r, wallTop - 0.9, Math.Max(3, (int)(w / 2.2)), 3, false, Looks.WoodDark, 9u, lod);
            }
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY, f.GroundY + h, hw, hd, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = h;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A bell pavilion: a moulded plinth, four carved posts, a hipped tile roof with struts (to
        /// <paramref name="totalM"/>) and the great bronze bell hung under it.</summary>
        public static int BellPavilion(float w, float d, float totalM, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            float roofRise = (float)Math.Min(2.2, 0.2 * Math.Max(w, d));
            float eaveM = Math.Max(2.4f, totalM - roofRise);
            double hw = 0.5 * w, hd = 0.5 * d;
            SacredParts.PlinthLevel(m, k, hw, hd, 0, 0.6, 0.3, Looks.Stone, Looks.StoneLight, lod);
            double pu = hw - 0.6, pw = hd - 0.6;
            for (int q = 0; q < 4; q++)
                SacredParts.Post(m, k, (q % 2 == 0 ? -1 : 1) * pu, (q < 2 ? -1 : 1) * pw, 0.6, eaveM - 0.3, 0.3, Looks.WoodDark, lod);
            SacredDraw.Box(m, k, -pu - 0.2, pu + 0.2, eaveM - 0.3, eaveM, -pw - 0.2, pw + 0.2, Looks.WoodDark, BoxFaces.All);
            RoofSpec r = SacredParts.HipRoof(m, k, pu + 0.2, pw + 0.2, eaveM + 0.25, Math.Max(0.6, hw - pu), 0.3, totalM, false, 0, false, lod);
            if (lod <= 1) SacredParts.Struts(m, k, r, eaveM - 0.25, 2, 2, false, Looks.WoodDark, 3u, lod);
            Profile2 p = Prof;
            double bh = 1.8, br = 0.85, by = eaveM - 0.45 - bh;
            p.Add(0, 0).Add(br * 1.05, 0, true).Add(br, 0.08 * bh).Add(0.85 * br, 0.3 * bh).Add(0.7 * br, 0.65 * bh).Add(0.62 * br, 0.88 * bh).Add(0.35 * br, bh).Add(0, bh);
            Shapes.Lathe(m, SacredDraw.At(k, 0, by, 0), Looks.Bronze, p, SacredParts.Seg(16, lod));
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY - 0.3, f.GroundY + 0.6, hw, hd, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A hiti (stone water spouts; stage 1 keeps the ground level): a stone rim, steps descending on three
        /// sides to a dark stone floor, a back wall with a small deity niche over each carved makara spout and its stream.</summary>
        public static int Hiti(float w, float d, int spouts, bool flowing, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d;
            ShapeBrush stone = Looks.Stone, light = Looks.StoneLight;
            if (lod >= 3)
            {
                SacredDraw.Box(m, k, -hw, hw, -0.2, 0.5, -hd, hd, light, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }
            // Rim and descending steps (rings of decreasing size).
            int steps = lod == 0 ? 3 : 1;
            for (int q = 0; q < steps; q++)
            {
                double ins = 0.45 * q, top = 0.5 - 0.15 * q;
                SacredParts.PlinthLevel(m, k, hw - ins, hd - ins, -0.2, top, 0, q == 0 ? light : stone, light, Math.Max(lod, 2));
            }
            SacredDraw.CapRect(m, k, 0, 0, hw - 0.45 * steps, hd - 0.45 * steps, 0.06 + 0.5 - 0.15 * steps, true, Looks.Of(MeshColor.FromHex(0x4A4E48), MaterialChannel.Flagstone, 0.7f));
            // Back wall with niches and spouts.
            double bw = -hd + 0.2;
            SacredDraw.Box(m, k, -hw, hw, -0.2, 1.9, bw - 0.4, bw, light, BoxFaces.All & ~BoxFaces.Bottom);
            int n = Math.Max(1, spouts);
            for (int q = 0; q < n; q++)
            {
                double u = -hw + w * (q + 0.5) / n;
                if (lod <= 1)
                {
                    SacredDraw.Panel(m, k, u - 0.25, 1.2, u + 0.25, 1.7, bw + 0.005, Looks.Recess);
                    ShikharaGenerator.Arch(m, k, u, 1.7, bw + 0.005, 0.56, 0.2, 0.05, 0.05, true, light, lod + 1);
                }
                Path3 sp = new Path3(4);
                sp.Add(u, 0.95, bw).Add(u, 0.98, bw + 0.45).Add(u, 0.9, bw + 0.8);
                Shapes.Tube(m, k, stone, sp, 0.14, lod == 0 ? 6 : 4, true, false, default, 0.08);
                if (lod == 0) Shapes.Ellipsoid(m, SacredDraw.At(k, u, 1.0, bw + 0.25), stone, 0.2, 0.17, 0.22, 4, false);
                if (flowing && lod <= 1)
                    SacredDraw.Quad(m, k, u - 0.05, 0.88, bw + 0.82, u + 0.05, 0.88, bw + 0.82, u + 0.07, 0.07, bw + 0.95, u - 0.07, 0.07, bw + 0.95, 0, 0.1, 1,
                                    Looks.Of(MeshColor.FromHex(0x9CC9E0), MaterialChannel.Water));
            }
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY - 0.2, f.GroundY + 0.5, hw, hd, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A bahal or palace courtyard: a quadrangle of brick ranges with carved windows and tile roofs around a
        /// walkable paved court, the gate in the middle of the front range (torana and painted lions), the court open to
        /// the sky. Returns the court's inner half sizes through <paramref name="innerHalfU"/> and
        /// <paramref name="innerHalfW"/>.</summary>
        public static int Courtyard(float w, float d, float rangeDepthM, float heightM, bool lions, uint wall, in GenFrame f, int lod, MeshData m, GenColliders c,
                                    out double innerHalfU, out double innerHalfW)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d, r = Math.Min(rangeDepthM, 0.3 * Math.Min(w, d));
            innerHalfU = hw - r;
            innerHalfW = hd - r;
            double gate = 1.0;
            ShapeBrush wb = wall != 0 ? Looks.Of(wall, MaterialChannel.BrickGlazed) : Looks.WallGlazed;
            Range(m, k, kf, -hw, -gate, hd - r, hd, heightM, wb, c, lod);
            Range(m, k, kf, gate, hw, hd - r, hd, heightM, wb, c, lod);
            Range(m, k, kf, -hw, hw, -hd, -hd + r, heightM, wb, c, lod);
            Range(m, k, kf, -hw, -hw + r, -hd + r, hd - r, heightM, wb, c, lod);
            Range(m, k, kf, hw - r, hw, -hd + r, hd - r, heightM, wb, c, lod);
            SacredDraw.Box(m, k, -gate, gate, 2.0, heightM, hd - r, hd, wb, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Panel(m, k, -gate, 0, gate, 2.0, hd - r + 0.01, Looks.Ao(Looks.WoodDark, 0.5f));
            if (lod <= 1) SacredParts.Torana(m, k, 0, 2.0, hd + 0.01, 2.4, Looks.Gilt, lod);
            if (lions && lod <= 1)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * (gate + 0.7), 0, hd + 0.7, 0.32, 0.4, 0.5, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * (gate + 0.7), 0.5, hd + 0.7, 1.15, GuardianKind.Lion, true, true, lod);
                }
            }
            // Court paving (walkable).
            SacredDraw.CapRect(m, k, 0, 0, innerHalfU, innerHalfW, 0.05, true, Looks.Of(MeshColor.FromHex(0x9A8F84), MaterialChannel.Flagstone));
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY - 0.3, f.GroundY + 0.05, innerHalfU, innerHalfW, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        private static void Range(MeshData m, in Affine3 k, in KitFrame kf, double u0, double u1, double w0, double w1, double h, in ShapeBrush wall, GenColliders c, int lod)
        {
            double cu = 0.5 * (u0 + u1), cw = 0.5 * (w0 + w1), hu = 0.5 * (u1 - u0), hw = 0.5 * (w1 - w0);
            Affine3 rk = SacredDraw.At(k, cu, 0, cw);
            int storeys = lod >= 2 ? 1 : Math.Max(1, (int)Math.Round(h / 2.8));
            double sh = h / storeys;
            for (int s = 0; s < storeys; s++) SacredParts.BandedWall(m, rk, hu, hw, s == 0 ? -0.3 : s * sh, (s + 1) * sh, wall, Looks.WoodDark, lod, true, s == storeys - 1);
            if (lod == 0)
            {
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 side = SacredDraw.Turn(rk, 0, 0, 0, sd * 0.5 * Math.PI);
                    double half = sd % 2 == 0 ? hw : hu, along = sd % 2 == 0 ? hu : hw;
                    int nw = (int)Math.Floor(2 * along / 2.4);
                    for (int s = 1; s < storeys; s++)
                        for (int q = 0; q < nw; q++)
                            SacredParts.LatticeWindow(m, side, -along + 2 * along * (q + 0.5) / nw, s * sh + 0.95, half, 0.75, 0.9, Looks.WoodDark, lod + 1, 2);
                }
            }
            SacredParts.HipRoof(m, rk, hu, hw, h + 0.2, 0.7, 0.25, h + 0.25 + 0.3 * Math.Min(hu, hw) + 0.4, false, 0, false, lod + 1);
            if (c != null)
            {
                double x, y, z;
                kf.ToWorld(cu, 0, cw, out x, out y, out z);
                c.AddBox(x, z, y - 0.3, y + h, hu, hw, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }
    }
}
