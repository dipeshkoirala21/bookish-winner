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

    }
}
