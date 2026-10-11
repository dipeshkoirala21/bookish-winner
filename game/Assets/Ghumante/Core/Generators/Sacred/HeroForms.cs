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

        /// <summary>A hero replica (richer figures).</summary>
        public bool Hero;

        /// <summary>The red cloth valance under the eaves (Kathmandu Durbar Square: Shiva-Parvati).</summary>
        public bool Fringe;

        /// <summary>A broad skirt roof round the walls over the ground storey, on struts, with its fringe (Bhimsen, Patan:
        /// the temple reads as two roofs).</summary>
        public bool SkirtRoof;

        /// <summary>Spacing multiplier of the repeated detail (≥ 1; 0 = 1), raised by <see cref="SacredSelector.Thin"/>
        /// so a large generic structure keeps its LOD0 form within the generic ceiling.</summary>
        public float DetailScale;
    }

    /// <summary>The generators for the hero forms that are not pagodas, stupas or shikharas (ref_temples 5): house-temples,
    /// the open-air relief, a pillar with a kneeling figure, palace towers, the Dharahara, gates, palace fronts, bell
    /// pavilions, hitis, bahal courts and the vajra. Each builds in a <see cref="GenFrame"/> (+W through the main door)
    /// from the detailed parts kit (mouldings, carved doors and windows, real roofs with struts and bells), writes the
    /// material channels and baked AO, and returns its triangles; heights match the recipe exactly.</summary>
    public static partial class HeroForms
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
            double hw = 0.5 * p.W, hd = 0.5 * p.D, ds = Math.Max(1.0, p.DetailScale);
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
                int bays = Math.Max(1, Math.Min(5, (int)Math.Round(2 * along / (1.9 * ds))));
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
                            if (sd == 0 && p.Figures && lod <= 1)
                            {
                                // Shiva-Parvati's front: a row of tall carved windows under cusped arches (refs shivaparvati 01, 06).
                                ArchedWindow(m, side, u, vs + 0.35, half, Math.Min(1.5, 2 * along / bays - 0.5), Math.Min(2.3, sH - 0.6), lod);
                                windows++;
                                continue;
                            }
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
            if (p.SkirtRoof)
            {
                // A lean-to skirt roof round all four walls from an eave 1.5 m out up to the wall above the ground storey.
                double sv = plinthTop + sH + 0.25, over = Math.Min(1.8, 0.14 * Math.Max(p.W, p.D) + 0.2), riseS = 0.55 * over;
                uint stile = p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile;
                var skirt = new RoofSpec
                {
                    EaveU = hw + over, EaveW = hd + over, EaveV = sv - riseS, TopU = hw + 0.02, TopW = hd + 0.02, TopV = sv, WallU = hw, WallW = hd,
                    WallTopV = sv - riseS + 0.1, Lift = 0.04 * (hw + over), Sag = 0.01 * over, FasciaH = 0.18, CoursePitch = 0.62 * ds, FringePitch = 0.7 * ds,
                    Fringe = p.Fringe, FringeH = 0.3, Horns = true,
                    Roof = Looks.Of(stile, MaterialChannel.RoofTile), Ridge = Looks.Of(MeshColor.Scale(stile, 0.72f), MaterialChannel.RoofTile), Fascia = Looks.WoodDark,
                    Soffit = Looks.Ao(Looks.WoodDark, 0.55f), Horn = Looks.WoodDark,
                };
                SacredParts.Roof(m, k, skirt, lod);
                if (lod <= 1)
                {
                    SacredParts.Struts(m, k, skirt, plinthTop + 0.55 * sH, PagodaGenerator.StrutsPerSide(2 * hw, 1.3 * ds), PagodaGenerator.StrutsPerSide(2 * hd, 1.3 * ds), false,
                                       Looks.WoodDark, 37u, lod);
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
            RoofSpec r = SacredParts.HipRoof(m, k, hw, hd, wallTop, Math.Min(1.4, 0.12 * Math.Max(p.W, p.D) + 0.4), 0.35, apex, false, tile, p.Fringe, lod);
            if (lod <= 1)
            {
                SacredParts.Struts(m, k, r, wallTop - 1.3, PagodaGenerator.StrutsPerSide(2 * hw, 1.1 * ds), PagodaGenerator.StrutsPerSide(2 * hd, 1.1 * ds), ds < 1.5, Looks.WoodDark, 31u, lod);
                SacredParts.Bells(m, k, r, 0.5 * ds, 1.0, false, lod);
            }
            // Gilt finials along the ridge: five on a long one (Shiva-Parvati), the middle one tallest.
            int fin = r.TopU > 1.2 ? 5 : r.TopU > 0.2 ? 3 : 1;
            for (int q = 0; q < fin; q++)
            {
                // Five stand close together at the middle of a long ridge (refs shivaparvati 03); three spread along a short one.
                double u = fin == 1 ? 0 : fin == 5 ? (q - 2) * Math.Min(0.75, 0.2 * r.TopU) : -0.85 * r.TopU + 1.7 * r.TopU * q / (fin - 1);
                double fh = q == fin / 2 ? finial : 0.75 * finial;
                SacredParts.Gajur(m, SacredDraw.At(k, u, 0, 0), apex - 0.05, fh + 0.05, q == fin / 2 ? 0.24 : 0.2, Looks.Gilt, lod);
            }
            if (p.Lions && lod <= 2)
            {
                double gw = hd + 0.6 + run - 0.2;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * 1.35, 0, gw, 0.3, 0.38, 0.5, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * 1.35, 0.5, gw, 1.2, GuardianKind.Lion, true, p.Hero ? SacredFigures.Hero : SacredFigures.Generic, lod, sgn);
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

        /// <summary>A tall carved window under a cusped arch at u on the wall plane w from sill height v: a dark opening
        /// with a carved lattice lower panel, a stepped wooden frame with lintel ears and the lobed arch over it.</summary>
        private static void ArchedWindow(MeshData m, in Affine3 k, double u, double v, double w, double width, double height, int lod)
        {
            double hu = 0.5 * width, archR = hu, top = v + height, spring = top - 0.75 * archR;
            SacredDraw.Panel(m, k, u - hu, v + 0.6, u + hu, spring, w + 0.01, Looks.Recess);
            SacredDraw.Box(m, k, u - hu, u + hu, v, v + 0.6, w, w + 0.06, Looks.WoodMid, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            if (lod == 0)
            {
                // The carved lattice of the lower panel.
                for (int q = 1; q < 4; q++)
                {
                    double lu = u - hu + width * q / 4;
                    SacredDraw.Box(m, k, lu - 0.025, lu + 0.025, v + 0.05, v + 0.55, w + 0.06, w + 0.1, Looks.WoodDark, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
            }
            // Jambs, the lintel with its ears and the sill.
            for (int sgn = -1; sgn <= 1; sgn += 2)
                SacredDraw.Box(m, k, u + sgn * hu - 0.09, u + sgn * hu + 0.09, v, spring, w, w + 0.12, Looks.WoodDark, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, k, u - hu - 0.3, u + hu + 0.3, v - 0.12, v, w, w + 0.14, Looks.WoodDark, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, k, u - hu - 0.3, u + hu + 0.3, top, top + 0.16, w, w + 0.14, Looks.WoodDark, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Panel(m, k, u - hu, spring, u + hu, top, w + 0.005, Looks.WoodMid);
            ShikharaGenerator.Arch(m, k, u, spring, w + 0.04, width, 0.75 * archR, 0.12, 0.1, true, Looks.WoodDark, lod);
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

        /// <summary>Storey cornices of the 2021 Dharahara below the gallery (heights in metres of the 72 m tower,
        /// measured on photos from Sundhara: refs dharahara_new 00, 02, 05, 07; ref_assets-b.md §3).</summary>
        private static readonly double[] DharaharaBands = { 9.0, 15.2, 21.2, 27.2, 33.1, 39.1, 45.1, 51.0 };

        /// <summary>Shaft radius of the Dharahara at height <paramref name="y"/> (72 m scale): 6.0 m above the plinth
        /// tapering to 4.75 m under the gallery (1.26 : 1), flared to 6.35 m at its foot.</summary>
        private static double DharaharaR(double y)
        {
            double r = 6.0 - 1.25 * (y - 1.4) / (55.6 - 1.4);
            if (y < 2.4) r += 0.35 * (2.4 - y);
            return r;
        }

        /// <summary>
        /// The 2021 Dharahara at Sundhara (rebuilt "in the old style with a larger diameter", 22 storeys, 72 m; refs
        /// dharahara_new; ref_assets-b.md §3): a stepped round stone plinth; a white tapering shaft (1.26 : 1)
        /// telescoping in nine sections, each storey band a projecting cream cornice with an undercut cove, round
        /// porthole windows in each section and the arched door at the foot; the corbelled balcony gallery at 57.5 m
        /// with its white balustrade; the drum with eight arched openings and a cornice, a collar and the white bell
        /// dome; the gilt kalash and needle spire to exactly <paramref name="totalM"/>. LOD 0-2 keep every cornice (the
        /// tower is seen across the valley); LOD 3 (≤ 200 triangles) is the same silhouette on six sides.
        /// </summary>
        public static int Dharahara(float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            double s = totalM / 72.0;
            int sides = lod <= 0 ? 40 : lod == 1 ? 20 : lod == 2 ? 8 : 6;
            ShapeBrush white = Looks.Of(MeshColor.FromHex(0xF7F5EF), MaterialChannel.Plaster), cream = Looks.Of(MeshColor.FromHex(0xF0E6CC), MaterialChannel.Plaster);
            ShapeBrush stone = Looks.Of(MeshColor.FromHex(0x8E8A82), MaterialChannel.Stone), glass = Looks.Of(MeshColor.FromHex(0x34404E), MaterialChannel.Glass);
            Affine3 ks = Affine3.Scaling(s).Then(k);

            // Stepped round plinth (three 0.45 m steps; refs dharahara_new 03-05).
            Profile2 pb = Prof;
            if (lod >= 3) pb.Add(8.0, -0.3, true).Add(8.0, 1.4, true).Add(0, 1.4);
            else if (lod == 2) pb.Add(0, -0.3).Add(8.0, -0.3, true).Add(8.0, 1.4, true).Add(0, 1.4);
            else
                pb.Add(0, -0.3).Add(8.6, -0.3, true).Add(8.6, 0.45, true).Add(8.0, 0.45, true).Add(8.0, 0.9, true).Add(7.4, 0.9, true).Add(7.4, 1.4, true)
                  .Add(0, 1.4);
            Shapes.Lathe(m, ks, stone, pb, sides);

            // Shaft sections and storey cornices.
            int nb = DharaharaBands.Length;
            if (lod >= 3)
            {
                // Far: one white lathe with every other cornice as a sharp shelf.
                Profile2 pf = Prof;
                pf.Add(DharaharaR(1.4), 1.4, true);
                for (int b = 1; b < nb; b += 2)
                {
                    double yb = DharaharaBands[b], rb = DharaharaR(yb);
                    pf.Add(rb + 0.35, yb, true).Add(rb, yb + 0.25, true);
                }
                pf.Add(DharaharaR(55.6), 55.6, true).Add(7.0, 57.4, true).Add(4.5, 57.7, true).Add(4.5, 61.3, true).Add(3.95, 62.4, true).Add(0.4, 66.1, true);
                Shapes.Lathe(m, ks, white, pf, sides);
                Profile2 ps = Prof;
                ps.Add(0.45, 66.0, true).Add(0.12, 67.9).Add(0, 72.0);
                Shapes.Lathe(m, ks, Looks.Gilt, ps, 3);
            }
            else
            {
                double y0 = 1.4;
                for (int b = 0; b <= nb; b++)
                {
                    double y1 = b < nb ? DharaharaBands[b] : 55.6;
                    // The section: tapering a little faster than the tower, so each band oversails the one above
                    // (the telescoped look of the photos); the foot of the bottom section flares onto the plinth.
                    Profile2 sec = Prof;
                    double rTop = DharaharaR(y1) - (b < nb ? 0.1 : 0.0), yTop = b < nb ? y1 - (lod == 2 ? 0.3 : 0.42) : y1;
                    if (b == 0 && lod == 2) sec.Add(6.3, 1.4, true);
                    else if (b == 0) sec.Add(6.4, 1.4, true).Add(6.3, 1.65).Add(DharaharaR(2.4), 2.4);
                    else sec.Add(DharaharaR(y0) - 0.05, y0, true);
                    sec.Add(rTop, yTop, true);
                    Shapes.Lathe(m, ks, white, sec, sides);
                    if (b == nb) break;
                    double yb = y1, rb = DharaharaR(yb);
                    Profile2 cor = Prof;
                    if (lod == 2) cor.Add(rb - 0.1, yb - 0.3, true).Add(rb + 0.34, yb, true).Add(rb - 0.05, yb + 0.2, true);
                    else
                        cor.Add(rb - 0.1, yb - 0.42, true).Add(rb + 0.1, yb - 0.32).Add(rb + 0.28, yb - 0.14).Add(rb + 0.36, yb + 0.02).Add(rb + 0.3, yb + 0.15)
                           .Add(rb + 0.14, yb + 0.21, true).Add(rb - 0.05, yb + 0.22, true);
                    Shapes.Lathe(m, ks, cream, cor, sides);
                    y0 = yb + (lod == 2 ? 0.2 : 0.22);
                }

                // Gallery: a concave corbel out to the balcony slab with a rounded edge, the deck, the balustrade.
                Profile2 pg = Prof;
                if (lod == 2) pg.Add(DharaharaR(55.6), 55.6, true).Add(5.6, 56.8).Add(7.0, 57.3, true).Add(6.95, 57.66, true).Add(4.5, 57.68, true);
                else
                    pg.Add(DharaharaR(55.6), 55.6, true).Add(4.85, 55.95).Add(5.4, 56.45).Add(6.3, 56.85).Add(6.95, 56.95, true).Add(7.05, 57.3)
                      .Add(6.95, 57.66, true).Add(4.5, 57.68, true);
                Shapes.Lathe(m, ks, white, pg, sides);
                double railR = 6.78, deck = 57.68, railTop = deck + 1.05;
                if (lod <= 1)
                {
                    // White balustrade (photos: a pale lattice round the deck): a low solid parapet, balusters, a
                    // rounded top rail.
                    Profile2 rail = Prof;
                    rail.Clear(true);
                    rail.Add(railR - 0.1, railTop - 0.12, true).Add(railR + 0.1, railTop - 0.12, true).Add(railR + 0.12, railTop - 0.04).Add(railR, railTop)
                        .Add(railR - 0.12, railTop - 0.04);
                    Shapes.Lathe(m, ks, white, rail, sides);
                    Profile2 low = Prof;
                    low.Clear(true);
                    low.Add(railR - 0.1, deck, true).Add(railR + 0.1, deck, true).Add(railR + 0.1, deck + 0.38, true).Add(railR - 0.1, deck + 0.38, true);
                    Shapes.Lathe(m, ks, white, low, sides);
                    int posts = lod == 0 ? 48 : 24;
                    for (int q = 0; q < posts; q++)
                    {
                        double a = 2 * Math.PI * (q + 0.5) / posts;
                        Shapes.Cylinder(m, SacredDraw.At(ks, railR * Math.Sin(a), deck + 0.36, railR * Math.Cos(a)), white, lod == 0 ? 0.075 : 0.09, railTop - deck - 0.46,
                                        lod == 0 ? 6 : 4, 0, 0, false, false);
                    }
                }
                else
                {
                    // A solid low balustrade ring reads from afar.
                    Profile2 bal = Prof;
                    bal.Clear(true);
                    bal.Add(railR - 0.06, deck, true).Add(railR + 0.06, deck, true).Add(railR + 0.06, railTop - 0.1, true).Add(railR - 0.06, railTop - 0.1, true);
                    Shapes.Lathe(m, ks, white, bal, sides);
                }

                // Drum, drum cornice, collar, dome.
                Profile2 pd = Prof;
                pd.Add(4.5, 57.68, true).Add(4.5, 60.9, true);
                if (lod == 2) pd.Add(4.84, 61.1, true).Add(4.2, 61.32, true).Add(4.2, 62.36, true).Add(3.95, 62.36, true);
                Shapes.Lathe(m, ks, white, pd, sides);
                if (lod <= 1)
                {
                    Profile2 dc = Prof;
                    dc.Add(4.5, 60.9, true).Add(4.72, 61.0).Add(4.84, 61.15).Add(4.76, 61.3, true).Add(4.2, 61.32, true);
                    Shapes.Lathe(m, ks, cream, dc, sides);
                    Profile2 col = Prof;
                    col.Add(4.2, 61.32, true).Add(4.2, 62.05, true).Add(4.38, 62.15).Add(4.36, 62.32, true).Add(3.95, 62.36, true);
                    Shapes.Lathe(m, ks, white, col, sides);
                }
                Profile2 dome = Prof;
                dome.Add(3.95, 62.36, true).Add(3.98, 62.9).Add(3.82, 63.7).Add(3.32, 64.6).Add(2.5, 65.3).Add(1.4, 65.8).Add(0.5, 66.05).Add(0.36, 66.1, true);
                if (lod == 2)
                {
                    dome.Clear(false);
                    dome.Add(3.95, 62.36, true).Add(3.9, 63.4).Add(3.0, 64.9).Add(1.5, 65.75).Add(0.36, 66.1, true);
                }
                Shapes.Lathe(m, ks, white, dome, sides);

                // Gilt kalash and needle spire.
                Profile2 fin = Prof;
                fin.Add(0.36, 66.0, true).Add(0.48, 66.05, true).Add(0.64, 66.3).Add(0.68, 66.62).Add(0.52, 66.95).Add(0.22, 67.15, true).Add(0.36, 67.4)
                   .Add(0.3, 67.66).Add(0.12, 67.86, true).Add(0.07, 70.6).Add(0, 72.0);
                if (lod == 2)
                {
                    fin.Clear(false);
                    fin.Add(0.36, 66.0, true).Add(0.66, 66.5).Add(0.22, 67.15, true).Add(0.12, 67.86, true).Add(0, 72.0);
                }
                Shapes.Lathe(m, ks, Looks.Gilt, fin, lod == 0 ? 12 : lod == 1 ? 8 : 5);

                int windows = 0;
                if (lod <= 1)
                {
                    // Arched openings round the drum (people look out over the valley from the deck behind them).
                    for (int q = 0; q < 8; q++)
                    {
                        Affine3 side = SacredDraw.Turn(ks, 0, 0, 0, q * 0.25 * Math.PI + 0.125 * Math.PI);
                        ArchPanel(m, side, 0, 58.15, 1.15, 2.05, 4.51, glass, lod == 0 ? 6 : 2);
                        windows++;
                    }
                    // The arched door at the foot (+W) and the round porthole windows of each section.
                    ArchPanel(m, ks, 0, 1.4, 1.7, 2.7, DharaharaR(2.6) + 0.02, Looks.Of(MeshColor.FromHex(0x5A3A26), MaterialChannel.Wood), lod == 0 ? 6 : 2);
                    if (lod == 0)
                    {
                        double yPrev = 1.4;
                        for (int b = 0; b <= nb; b++)
                        {
                            double yTop = b < nb ? DharaharaBands[b] : 55.6, ym = 0.5 * (yPrev + yTop) + (b == 0 ? 1.2 : 0.3);
                            int per = b == 0 ? 3 : 4;
                            for (int q = 0; q < per; q++)
                            {
                                double a = 2 * Math.PI * q / per + (b % 2 == 0 ? 0.25 * Math.PI : 0.0) + (b == 0 ? 0.5 * Math.PI : 0.0);
                                double r = DharaharaR(ym) - 0.07 * (ym - yPrev) / (yTop - yPrev) - 0.05, dx = Math.Sin(a), dz = Math.Cos(a);
                                Affine3 at = SacredDraw.Basis(ks, r * dx, ym, r * dz, 0, 1, 0, dx, 0, dz);
                                Shapes.Cylinder(m, at, cream, 0.34, 0.13, 10, 0.03, 1, false, true);
                                Shapes.Cylinder(m, at, glass, 0.24, 0.15, 10, 0, 0, false, true);
                                windows++;
                            }
                            yPrev = yTop;
                        }
                    }
                }
                if (stats != null) stats.Windows = windows;
            }
            if (c != null)
            {
                // The plinth's three steps are walkable; the shaft is a solid round tower.
                for (int q = 0; q < 3; q++)
                {
                    double half = (8.6 - 0.6 * q) * 0.82 * s;
                    c.AddBox(f.X, f.Z, f.GroundY - 0.3, f.GroundY + 0.45 * (q + 1) * s + (q == 2 ? 0.05 * s : 0), half, half, 1, 0, GenColliderFlags.Walkable, GenColliders.Stone);
                }
                c.AddCylinder(f.X, f.Z, f.GroundY + 1.3 * s, f.GroundY + totalM, 6.3 * s);
            }
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.PlinthTopM = (float)(1.4 * s);
                stats.DoorYawDeg = f.YawDeg;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A vertical arched panel facing +w at depth <paramref name="w"/>: a rectangle from
        /// <paramref name="v0"/> up to the springing line and a semicircular head of <paramref name="segs"/> fan
        /// triangles (doors, drum openings).</summary>
        private static void ArchPanel(MeshData m, in Affine3 k, double u, double v0, double width, double height, double w, in ShapeBrush b, int segs)
        {
            double hw = 0.5 * width, spring = v0 + height - hw;
            SacredDraw.Panel(m, k, u - hw, v0, u + hw, spring, w, b);
            int cv = SacredDraw.V(m, k, u, spring, w, 0, 0, 1, b), prev = -1;
            for (int q = 0; q <= segs; q++)
            {
                double a = Math.PI * q / segs;
                int v = SacredDraw.V(m, k, u + hw * Math.Cos(a), spring + hw * Math.Sin(a), w, 0, 0, 1, b);
                if (prev >= 0) SacredDraw.Tri(m, cv, prev, v);
                prev = v;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gates and palace fronts
        // ---------------------------------------------------------------------------------------------------------

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

        /// <summary>Thickness of a hiti's parapet wall (OSM w328923444: 0.4 m).</summary>
        private const double HitiParapetT = 0.4;

        /// <summary>Parapet height above the square (OSM w328923444: 0.8 m).</summary>
        private const double HitiParapetH = 0.8;

        /// <summary>
        /// Depth of a hiti's terrace level and its spout court below the square for a pit of
        /// <paramref name="w"/> × <paramref name="d"/> metres: Manga Hiti (about 15 × 15 m) has its terrace at −2.5 m and
        /// the court at −3.5 m (OSM walls w328923444 / w328923445; temples.md 2.9: about 3 m); small neighbourhood
        /// hitis are shallower.
        /// </summary>
        public static void HitiLevels(float w, float d, out double terraceV, out double courtV)
        {
            double m = Math.Min(w, d);
            terraceV = -Math.Min(2.5, 0.85 + 0.22 * m);
            courtV = terraceV - Math.Min(1.0, 0.3 + 0.08 * m);
        }

        /// <summary>
        /// The opening a hiti cuts into the ground, in its frame (centred, half extents along U and W): the inside of the
        /// parapet. The terrain, the paved areas and the ground query must leave this rectangle open (the pit lies
        /// below grade), as they do under a hero plinth; see docs/research/w2/ref_assets-b.md §4 (open issue).
        /// </summary>
        public static void HitiGroundCut(float w, float d, out double halfU, out double halfW)
        {
            halfU = Math.Max(0.5, 0.5 * w - HitiParapetT);
            halfW = Math.Max(0.5, 0.5 * d - HitiParapetT);
        }

        /// <summary>
        /// A hiti (dhunge dhara; temples.md 2.9; Manga Hiti refs mangahiti_c, ref_assets-b.md §4): a <b>sunken stepped
        /// stone pit</b> below the square. A low brick parapet with a stone coping rings the opening (0.8 m), broken on
        /// the front (+W) by the main stair; brick walls line the pit down to a brick-paved terrace ledge, from which
        /// stone flights step down on the two sides and the stair continues to the flagstone spout court; the back (−W)
        /// wall carries <paramref name="spouts"/> carved stone makara spouts, each under a horseshoe stela with a deity
        /// niche, pouring into the water channel at its foot (when <paramref name="flowing"/>), and a round stone basin
        /// stands in the court. With <paramref name="maniMandap"/> the two Mani Mandap pavilions (1700, on their brick
        /// platform) stand in front of the stair beyond the parapet, as at Patan Durbar Square (OSM w328706342,
        /// w328706352). Walkable colliders follow the terrace, court and stairs down into the pit; the parapet blocks.
        /// The ground inside <see cref="HitiGroundCut"/> must be cut by the terrain and area meshers.
        /// </summary>
        public static int Hiti(float w, float d, int spouts, bool flowing, in GenFrame f, int lod, MeshData m, GenColliders c, bool maniMandap = false)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double hw = 0.5 * w, hd = 0.5 * d, pt = HitiParapetT, ph = HitiParapetH;
            double tv, cv;
            HitiLevels(w, d, out tv, out cv);
            ShapeBrush brick = Looks.Of(MeshColor.FromHex(0xB5533A), MaterialChannel.Brick), coping = Looks.Of(MeshColor.FromHex(0x9A958B), MaterialChannel.Stone);
            ShapeBrush stone = Looks.Of(MeshColor.FromHex(0x8C887C), MaterialChannel.Stone), mossy = Looks.Of(MeshColor.FromHex(0x737A62), MaterialChannel.Stone);
            ShapeBrush paving = Looks.Of(MeshColor.FromHex(0xA65A40), MaterialChannel.Brick, 0.85f), flag = Looks.Of(MeshColor.FromHex(0x66665C), MaterialChannel.Flagstone, 0.8f);
            ShapeBrush water = Looks.Of(MeshColor.FromHex(0x4F6E5A), MaterialChannel.Water);
            // Inside of the parapet (the opening), the terrace ledge and the court.
            double iu = hw - pt, iw = hd - pt;
            double ledge = Math.Max(0.6, Math.Min(2.8, 0.18 * Math.Min(w, d)));
            double cu = iu - ledge, cwB = -iw + Math.Max(0.5, 0.6 * ledge), cwF = iw - ledge;
            double stairHalf = Math.Max(0.6, Math.Min(0.9, 0.08 * w));
            if (lod >= 3)
            {
                // Far: the parapet ring round a dark opening.
                ParapetRing(m, k, hw, hd, pt, 0, ph, 0, brick, coping, 3);
                SacredDraw.Quad(m, k, -iu, -0.6, -iw, iu, -0.6, -iw, iu, -0.6, iw, -iu, -0.6, iw, 0, 1, 0, Looks.Ao(flag, 0.45f));
                if (maniMandap) ManiMandap(m, k, hd, lod, null, kf, f);
                SacredDraw.Bake(m, v0, i0, f.GroundY + cv);
                return m.TriangleCount - t0;
            }
            // Parapet (brick, stone coping) broken by the stair at the front.
            ParapetRing(m, k, hw, hd, pt, stairHalf, ph, -0.05, brick, coping, lod);
            // Pit walls: brick from the square down to the terrace, all round the inside of the parapet (the front one
            // broken by the stair).
            ShapeBrush brickIn = Looks.Ao(brick, 0.8f);
            Affine3 back = SacredDraw.Turn(k, 0, 0, 0, Math.PI);
            SacredDraw.Panel(m, back, -iu, tv, -stairHalf, 0, iw, brickIn);                                 // front wall (facing −W), right of the stair
            SacredDraw.Panel(m, back, stairHalf, tv, iu, 0, iw, brickIn);                                   // and left of it
            SacredDraw.Panel(m, k, -iu, tv, iu, 0, -iw, brickIn);                                            // back wall, facing +W
            SacredDraw.Panel(m, SacredDraw.Turn(k, 0, 0, 0, 0.5 * Math.PI), -iw, tv, iw, 0, -iu, brickIn);  // −U wall, facing +U
            SacredDraw.Panel(m, SacredDraw.Turn(k, 0, 0, 0, -0.5 * Math.PI), -iw, tv, iw, 0, -iu, brickIn); // +U wall, facing −U
            if (lod <= 1)
            {
                // Stone string course a little under the square, as in the photos (mangahiti_c 14).
                double band = -0.55;
                SacredDraw.Box(m, k, -iu, iu, band - 0.18, band, -iw, -iw + 0.08, coping, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                SacredDraw.Box(m, k, -iu, -iu + 0.08, band - 0.18, band, -iw, iw, coping, BoxFaces.Right | BoxFaces.Top | BoxFaces.Bottom);
                SacredDraw.Box(m, k, iu - 0.08, iu, band - 0.18, band, -iw, iw, coping, BoxFaces.Left | BoxFaces.Top | BoxFaces.Bottom);
            }
            // Terrace ledge (brick paving) round the court; stone flights step down from it on both sides.
            int sideSteps = lod == 0 ? Math.Max(2, (int)Math.Round((tv - cv) / 0.25)) : lod == 1 ? 2 : 1;
            double run = Math.Min(0.75 * ledge, 0.3 * sideSteps), su = cu + run;
            SacredDraw.Quad(m, k, -iu, tv, -iw, iu, tv, -iw, iu, tv, cwB, -iu, tv, cwB, 0, 1, 0, paving);            // back ledge
            SacredDraw.Quad(m, k, -iu, tv, cwF, -stairHalf, tv, cwF, -stairHalf, tv, iw, -iu, tv, iw, 0, 1, 0, paving); // front ledge, either side of the stair
            SacredDraw.Quad(m, k, stairHalf, tv, cwF, iu, tv, cwF, iu, tv, iw, stairHalf, tv, iw, 0, 1, 0, paving);
            SacredDraw.Quad(m, k, -iu, tv, cwB, -su, tv, cwB, -su, tv, cwF, -iu, tv, cwF, 0, 1, 0, paving);            // side ledges
            SacredDraw.Quad(m, k, su, tv, cwB, iu, tv, cwB, iu, tv, cwF, su, tv, cwF, 0, 1, 0, paving);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Flight(m, k, false, cwB, cwF, sgn * su, sgn * cu, tv, cv, sideSteps, stone, Looks.Ao(mossy, 0.85f));
            // Court floor (flagstones) and the stone face of the front ledge down to it.
            SacredDraw.Quad(m, k, -cu, cv, cwB, cu, cv, cwB, cu, cv, cwF, -cu, cv, cwF, 0, 1, 0, flag);
            SacredDraw.Panel(m, back, -cu, cv, -stairHalf, tv, -cwF, mossy);
            SacredDraw.Panel(m, back, stairHalf, cv, cu, tv, -cwF, mossy);
            // Back: the stone spout wall with its stelae and makara spouts, the water channel at its foot.
            SacredDraw.Panel(m, k, -su, cv, su, tv, cwB, mossy);
            SacredDraw.Box(m, k, -iu, iu, tv - 0.02, tv + 0.12, cwB - 0.05, cwB + 0.12, coping, BoxFaces.Front | BoxFaces.Top);
            int n = Math.Max(1, spouts);
            double chanW = Math.Min(1.0, 0.14 * d), fall = Math.Min(0.94, chanW - 0.08);
            SacredDraw.Quad(m, k, -cu, cv - 0.18, cwB, cu, cv - 0.18, cwB, cu, cv - 0.18, cwB + chanW, -cu, cv - 0.18, cwB + chanW, 0, 1, 0, water);
            SacredDraw.Panel(m, back, -cu, cv - 0.2, cu, cv, -(cwB + chanW), Looks.Ao(stone, 0.7f));
            double spoutY = cv + Math.Min(1.0, 0.8 * (tv - cv) + 0.1);
            for (int q = 0; q < n; q++)
            {
                double u = -cu + 2 * cu * (q + 0.5) / n;
                Spout(m, k, u, spoutY, cwB, Math.Min(1.0, 1.6 * cu / n), tv, stone, mossy, lod);
                if (flowing && lod <= 1)
                    SacredDraw.Quad(m, k, u - 0.05, spoutY - 0.08, cwB + 0.86, u + 0.05, spoutY - 0.08, cwB + 0.86, u + 0.07, cv - 0.16, cwB + fall, u - 0.07, cv - 0.16, cwB + fall,
                                    0, 0.1, 1, Looks.Of(MeshColor.FromHex(0xA8D2E6), MaterialChannel.Water));
            }
            // Main stair: from the parapet gap at the front straight down over the ledge to the court, between cheek walls.
            int mainSteps = lod == 0 ? Math.Max(4, (int)Math.Round(-cv / 0.26)) : lod == 1 ? 5 : 1;
            double runMain = Math.Min(-cv / 0.26 * 0.28, iw - cwB - 1.6), wFootMain = hd - pt - runMain;
            if (lod >= 2) SacredDraw.Quad(m, k, -stairHalf, 0, hd, stairHalf, 0, hd, stairHalf, cv, wFootMain, -stairHalf, cv, wFootMain, 0, runMain, -cv, stone);
            else Flight(m, k, true, -stairHalf, stairHalf, hd, wFootMain, 0, cv, mainSteps, stone, Looks.Ao(stone, 0.8f));
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double u = sgn * stairHalf;
                int a = SacredDraw.V(m, k, u, 0, iw, sgn, 0, 0, brickIn), b = SacredDraw.V(m, k, u, cv, wFootMain, sgn, 0, 0, brickIn);
                int cA = SacredDraw.V(m, k, u, cv, iw, sgn, 0, 0, brickIn);
                SacredDraw.Tri(m, a, b, cA);
            }
            // A round stone basin in the court (mangahiti_c 04, 10).
            if (lod <= 1 && cu > 1.2)
            {
                Profile2 bp = Prof;
                bp.Add(0, 0).Add(0.42, 0, true).Add(0.48, 0.12).Add(0.46, 0.42, true).Add(0.36, 0.44, true).Add(0.34, 0.2, true).Add(0, 0.2);
                Shapes.Lathe(m, SacredDraw.At(k, 0.25 * cu, cv, 0.5 * (cwB + cwF) + 0.4), stone, bp, lod == 0 ? 14 : 8);
            }
            if (lod <= 1 && w >= 9)
            {
                // Black iron railings on the parapet beside the stair (mangahiti_c 01, 06).
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x23262B), MaterialChannel.Metal);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    double ua = sgn * (stairHalf + 0.2), ub = sgn * Math.Min(iu, stairHalf + 3.0);
                    Shapes.Bar(m, k, iron, ua, ph + 0.95, hd - 0.5 * pt, ub, ph + 0.95, hd - 0.5 * pt, 0.025, 4);
                    int posts = lod == 0 ? 8 : 3;
                    for (int q = 0; q <= posts; q++)
                    {
                        double u = ua + (ub - ua) * q / posts;
                        Shapes.Bar(m, k, iron, u, ph, hd - 0.5 * pt, u, ph + 0.95, hd - 0.5 * pt, 0.018, 4);
                    }
                }
            }
            if (lod == 0 && w >= 9)
            {
                // Potted plants along the back ledge above the spouts (mangahiti_c 02, 14).
                ShapeBrush pot = Looks.Of(MeshColor.FromHex(0xA4583A), MaterialChannel.Dirt), plant = Looks.Of(MeshColor.FromHex(0x4E7A34), MaterialChannel.Foliage);
                Profile2 pp = Prof;
                pp.Add(0, 0).Add(0.13, 0, true).Add(0.17, 0.22, true).Add(0.19, 0.26, true).Add(0.15, 0.27, true).Add(0, 0.25);
                int pots = (int)(2 * iu / 1.6);
                for (int q = 0; q < pots; q++)
                {
                    double u = -iu + 0.8 + (2 * iu - 1.6) * q / Math.Max(1, pots - 1), wq = 0.5 * (-iw + cwB);
                    Shapes.Lathe(m, SacredDraw.At(k, u, tv, wq), pot, pp, 8);
                    Shapes.Ellipsoid(m, SacredDraw.At(k, u, tv + 0.36, wq), plant, 0.2, 0.16, 0.2, 6, false);
                }
            }
            if (maniMandap) ManiMandap(m, k, hd, lod, c, kf, f);
            if (c != null)
            {
                double g = f.GroundY;
                // The parapet blocks; the ledge, court and stairs are walkable ground inside the pit.
                c.AddWall(kf.OX + kf.UX * -hw + kf.WX * -hd, kf.OZ + kf.UZ * -hw + kf.WZ * -hd, kf.OX + kf.UX * hw + kf.WX * -hd, kf.OZ + kf.UZ * hw + kf.WZ * -hd, g + cv, g + ph, 0.5 * pt);
                c.AddWall(kf.OX + kf.UX * -hw + kf.WX * -hd, kf.OZ + kf.UZ * -hw + kf.WZ * -hd, kf.OX + kf.UX * -hw + kf.WX * hd, kf.OZ + kf.UZ * -hw + kf.WZ * hd, g + cv, g + ph, 0.5 * pt);
                c.AddWall(kf.OX + kf.UX * hw + kf.WX * -hd, kf.OZ + kf.UZ * hw + kf.WZ * -hd, kf.OX + kf.UX * hw + kf.WX * hd, kf.OZ + kf.UZ * hw + kf.WZ * hd, g + cv, g + ph, 0.5 * pt);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    c.AddWall(kf.OX + kf.UX * sgn * stairHalf + kf.WX * hd, kf.OZ + kf.UZ * sgn * stairHalf + kf.WZ * hd, kf.OX + kf.UX * sgn * hw + kf.WX * hd,
                              kf.OZ + kf.UZ * sgn * hw + kf.WZ * hd, g + cv, g + ph, 0.5 * pt);
                // Ledge ring (four strips) and court.
                AddFrameBox(c, kf, 0, 0.5 * (-iw + cwB), iu, 0.5 * (cwB + iw), g + tv - 0.3, g + tv, GenColliders.Brick);
                AddFrameBox(c, kf, 0, 0.5 * (cwF + iw), iu, 0.5 * (iw - cwF), g + tv - 0.3, g + tv, GenColliders.Brick);
                AddFrameBox(c, kf, -0.5 * (iu + cu), 0.5 * (cwB + cwF), 0.5 * (iu - cu), 0.5 * (cwF - cwB), g + tv - 0.3, g + tv, GenColliders.Stone);
                AddFrameBox(c, kf, 0.5 * (iu + cu), 0.5 * (cwB + cwF), 0.5 * (iu - cu), 0.5 * (cwF - cwB), g + tv - 0.3, g + tv, GenColliders.Stone);
                AddFrameBox(c, kf, 0, 0.5 * (cwB + cwF), cu, 0.5 * (cwF - cwB), g + cv - 0.3, g + cv, GenColliders.Stone);
                // The main stair, from the square at the parapet gap down to the court.
                double wTop = hd, wFoot = hd - runMain - pt;
                c.AddRamp(new GenRamp
                {
                    X0 = kf.OX + kf.WX * wTop, Z0 = kf.OZ + kf.WZ * wTop, Y0 = (float)g, X1 = kf.OX + kf.WX * wFoot, Z1 = kf.OZ + kf.WZ * wFoot, Y1 = (float)(g + cv),
                    HalfWidth = (float)stairHalf, Material = GenColliders.Stone,
                });
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY + cv);
            return m.TriangleCount - t0;
        }

        /// <summary>A walkable frame-aligned box (centre (u, w) in the frame, half sizes, absolute heights).</summary>
        private static void AddFrameBox(GenColliders c, in KitFrame kf, double u, double w, double hu, double hw, double y0, double y1, byte material)
        {
            if (hu <= 0.05 || hw <= 0.05) return;
            c.AddBox(kf.OX + kf.UX * u + kf.WX * w, kf.OZ + kf.UZ * u + kf.WZ * w, y0, y1, hu, hw, kf.UX, kf.UZ, GenColliderFlags.Walkable, material);
        }

        /// <summary>The hiti parapet: four brick walls from <paramref name="v0"/> to <paramref name="h"/> with a stone
        /// coping, the front (+W) wall broken by a gap of half width <paramref name="gapHalf"/> for the stair.</summary>
        private static void ParapetRing(MeshData m, in Affine3 k, double hw, double hd, double t, double gapHalf, double h, double v0, in ShapeBrush brick,
                                        in ShapeBrush coping, int lod)
        {
            double cop = lod >= 2 ? 0 : 0.1;
            BoxFaces outer = lod >= 3 ? BoxFaces.Top | BoxFaces.Back | BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.All & ~BoxFaces.Bottom;
            // Back (−W) and the two sides run full length; the front is two pieces either side of the gap.
            SacredDraw.Box(m, k, -hw, hw, v0, h - cop, -hd, -hd + t, brick, outer);
            SacredDraw.Box(m, k, -hw, -hw + t, v0, h - cop, -hd + t, hd - t, brick, outer);
            SacredDraw.Box(m, k, hw - t, hw, v0, h - cop, -hd + t, hd - t, brick, outer);
            if (gapHalf > 0)
            {
                SacredDraw.Box(m, k, -hw, -gapHalf, v0, h - cop, hd - t, hd, brick, outer);
                SacredDraw.Box(m, k, gapHalf, hw, v0, h - cop, hd - t, hd, brick, outer);
            }
            else SacredDraw.Box(m, k, -hw, hw, v0, h - cop, hd - t, hd, brick, outer);
            if (cop <= 0) return;
            const double o = 0.04;
            SacredDraw.Box(m, k, -hw - o, hw + o, h - cop, h, -hd - o, -hd + t + o, coping, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, k, -hw - o, -hw + t + o, h - cop, h, -hd + t + o, hd - t - o, coping, BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, k, hw - t - o, hw + o, h - cop, h, -hd + t + o, hd - t - o, coping, BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, k, -hw - o, -gapHalf, h - cop, h, hd - t - o, hd + o, coping, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, k, gapHalf, hw + o, h - cop, h, hd - t - o, hd + o, coping, BoxFaces.All & ~BoxFaces.Bottom);
        }

        /// <summary>A flight of <paramref name="steps"/> stone steps in frame <paramref name="k"/>: along W
        /// (<paramref name="alongW"/>) or along U, spanning [<paramref name="a0"/>, <paramref name="a1"/>] across, from
        /// <paramref name="top"/> (at <paramref name="vTop"/>) down to <paramref name="foot"/> (at
        /// <paramref name="vFoot"/>) along: a riser facing the foot, then a tread, per step.</summary>
        private static void Flight(MeshData m, in Affine3 k, bool alongW, double a0, double a1, double top, double foot, double vTop, double vFoot, int steps,
                                   in ShapeBrush tread, in ShapeBrush riser)
        {
            double dir = foot > top ? 1 : -1;
            for (int q = 0; q < steps; q++)
            {
                double fa = (double)q / steps, fb = (double)(q + 1) / steps;
                double la = top + (foot - top) * fa, lb = top + (foot - top) * fb;
                double va = vTop + (vFoot - vTop) * fa, vb = vTop + (vFoot - vTop) * fb;
                if (alongW)
                {
                    SacredDraw.Quad(m, k, a0, vb, la, a1, vb, la, a1, va, la, a0, va, la, 0, 0, dir, riser);
                    SacredDraw.Quad(m, k, a0, vb, la, a1, vb, la, a1, vb, lb, a0, vb, lb, 0, 1, 0, tread);
                }
                else
                {
                    SacredDraw.Quad(m, k, la, vb, a0, la, vb, a1, la, va, a1, la, va, a0, dir, 0, 0, riser);
                    SacredDraw.Quad(m, k, la, vb, a0, la, vb, a1, lb, vb, a1, lb, vb, a0, 0, 1, 0, tread);
                }
            }
        }

        /// <summary>
        /// A carved stone makara spout on the hiti's back wall (face at w = <paramref name="wall"/>, facing +W): a
        /// horseshoe stela with a dark deity niche rising from just above the spout toward the ledge, and the makara
        /// head projecting about 0.75 m: a snout tapering forward from the wall and lifting at its tip, a curled
        /// upper lip, eyes and the ridge of its crest (LOD0); a tapered tube at LOD1, a block at LOD2.
        /// </summary>
        private static void Spout(MeshData m, in Affine3 k, double u, double v, double wall, double width, double ledgeV, in ShapeBrush stone, in ShapeBrush mossy,
                                  int lod)
        {
            double sw = Math.Max(0.7, Math.Min(1.25, width)), st0 = v + 0.15, sh = Math.Max(0.8, Math.Min(1.6, ledgeV - st0 + 0.7));
            if (lod <= 1)
            {
                // Stela: a slab with a semicircular head, extruded out of the wall.
                int segs = lod == 0 ? 10 : 4;
                double[] px = ShapeScratch.U, pz = ShapeScratch.W;
                int n = 0;
                double r = 0.5 * sw, spring = sh - r;
                px[n] = -r;
                pz[n++] = 0;
                px[n] = r;
                pz[n++] = 0;
                for (int q = 0; q <= segs; q++)
                {
                    double a = Math.PI * q / segs;
                    px[n] = r * Math.Cos(a);
                    pz[n++] = spring + r * Math.Sin(a);
                }
                // The polygon lies in the slab's local XZ (z up the wall: x = −U so that x × y = up); extrude along
                // +Y = out of the wall (+W).
                Affine3 slab = SacredDraw.Basis(k, u, st0, wall, -1, 0, 0, 0, 0, 1);
                Shapes.BevelExtrude(m, slab, stone, px, pz, n, 0.16, 0.04, lod == 0 ? 2 : 1, BevelStyle.Round, true, false);
                // The deity niche: a dark arched recess on the stela face.
                ArchPanel(m, k, u, st0 + 0.3 * sh, 0.42 * sw, 0.5 * sh, wall + 0.165, Looks.Recess, lod == 0 ? 5 : 2);
                // The makara: snout from the wall, tapering and lifting at the tip.
                Path3 sp = new Path3(5);
                sp.Add(u, v + 0.1, wall).Add(u, v + 0.02, wall + 0.34).Add(u, v - 0.03, wall + 0.62).Add(u, v + 0.06, wall + 0.85);
                Shapes.Tube(m, k, mossy, sp, 0.25, lod == 0 ? 10 : 6, true, false, default, 0.11);
                if (lod == 0)
                {
                    // Head mass with eyes, the crest ridge and the curled upper lip (trunk) at the tip.
                    Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.2, wall + 0.24), stone, 0.32, 0.28, 0.34, 10, false);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                        Shapes.Ellipsoid(m, SacredDraw.At(k, u + sgn * 0.17, v + 0.34, wall + 0.38), Looks.Of(MeshColor.FromHex(0x56544C), MaterialChannel.Stone), 0.075, 0.06, 0.06, 6,
                                         false);
                    Path3 crest = new Path3(4);
                    crest.Add(u, v + 0.46, wall + 0.05).Add(u, v + 0.52, wall + 0.3).Add(u, v + 0.38, wall + 0.5);
                    Shapes.Tube(m, k, stone, crest, 0.07, 5, true, false, default, 0.04);
                    Path3 lip = new Path3(5);
                    lip.Add(u, v + 0.08, wall + 0.82).Add(u, v + 0.28, wall + 0.96).Add(u, v + 0.4, wall + 0.84).Add(u, v + 0.3, wall + 0.72);
                    Shapes.Tube(m, k, mossy, lip, 0.08, 6, true, false, default, 0.045);
                }
            }
            else
            {
                SacredDraw.Box(m, k, u - 0.5 * sw, u + 0.5 * sw, st0, st0 + sh, wall, wall + 0.14, stone, BoxFaces.Front | BoxFaces.Top);
                SacredDraw.Box(m, k, u - 0.12, u + 0.12, v - 0.1, v + 0.12, wall, wall + 0.7, stone, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            }
        }

        /// <summary>
        /// The Mani Mandap (Patan Durbar Square, 1700; restored after 2015): two open royal pavilions side by side on a
        /// raised brick platform in front of Manga Hiti's stair (OSM w328706342 / w328706352: about 7 × 7 m roofs, 6 m
        /// high, four metres apart across the stair), each a ring of carved wooden posts on a brick plinth under a
        /// broad hipped tile roof on struts with a gilt finial.
        /// </summary>
        private static void ManiMandap(MeshData m, in Affine3 k, double hd, int lod, GenColliders c, in KitFrame kf, in GenFrame f)
        {
            const double size = 5.6, platformH = 0.9, eave = 3.6, apex = 6.0, overhang = 0.9;
            // Roofs about 6.9 m square (OSM), their eaves a little beyond the parapet, 1.3 m apart across the stair.
            double w = hd + 0.85 + 0.5 * size - 0.45 + 0.2 + overhang;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double u = sgn * 4.1;
                Affine3 p = SacredDraw.At(k, u, 0, w);
                double h = 0.5 * size;
                if (lod >= 3)
                {
                    SacredDraw.Box(m, p, -h, h, 0, eave, -h, h, Looks.Of(MeshColor.FromHex(0xB5533A), MaterialChannel.Brick), BoxFaces.All & ~BoxFaces.Bottom);
                    Lod3Roof(m, p, h + 0.8, h + 0.8, eave, apex, Looks.Tile);
                    continue;
                }
                SacredParts.PlinthLevel(m, p, h, h, 0, platformH, 0.3, Looks.PlinthBrick, Looks.Stone, lod);
                double pu = h - 0.45;
                int per = lod == 0 ? 4 : 2;
                for (int q = 0; q < per; q++)
                    for (int side = 0; side < 4; side++)
                    {
                        double t = -pu + 2 * pu * q / per;
                        double pu2 = side == 0 ? t : side == 1 ? pu : side == 2 ? -t : -pu, pw2 = side == 0 ? -pu : side == 1 ? t : side == 2 ? pu : -t;
                        SacredParts.Post(m, p, pu2, pw2, platformH, eave - 0.35, 0.26, Looks.WoodDark, lod);
                    }
                SacredDraw.Box(m, p, -pu - 0.2, pu + 0.2, eave - 0.35, eave, -pu - 0.2, pu + 0.2, Looks.WoodDark, BoxFaces.All);
                RoofSpec r = SacredParts.HipRoof(m, p, pu + 0.2, pu + 0.2, eave + 0.25, overhang, 0.3, apex - 0.35, false, 0, false, lod);
                if (lod <= 1) SacredParts.Struts(m, p, r, eave - 0.3, 3, 3, false, Looks.WoodDark, 5u, lod);
                SacredParts.Gajur(m, p, apex - 0.4, 0.45, 0.16, Looks.Gilt, lod);
                if (c != null)
                    c.AddBox(kf.OX + kf.UX * u + kf.WX * w, kf.OZ + kf.UZ * u + kf.WZ * w, f.GroundY - 0.3, f.GroundY + platformH, h, h, kf.UX, kf.UZ,
                             GenColliderFlags.Walkable, GenColliders.Brick);
            }
        }
    }
}
