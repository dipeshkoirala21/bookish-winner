using System;
using Ghumante.Core.Meshing;

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

    /// <summary>The generators for the hero forms that are not pagodas, stupas or shikharas: house-temples, the open-air
    /// relief, a pillar with a kneeling figure, palace towers, the Dharahara, gates, palace fronts, bell pavilions, hitis,
    /// bahal courts and the vajra. Each builds in a <see cref="GenFrame"/> (+W through the main door) and returns its
    /// triangles; heights match the recipe exactly.</summary>
    public static class HeroForms
    {
        // ---------------------------------------------------------------------------------------------------------
        // House-temple
        // ---------------------------------------------------------------------------------------------------------

        public static int HouseTemple(in HouseTempleParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double hw = 0.5 * p.W, hd = 0.5 * p.D;
            int levels = Math.Max(0, p.PlinthLevels);
            double rise = p.PlinthRiseM > 0 ? p.PlinthRiseM : 0.4, plinthTop = levels * rise;
            for (int i = 0; i < levels; i++)
            {
                double ins = 0.4 * i;
                MeshKit.Box(m, k, -hw - 0.6 + ins, hw + 0.6 - ins, i == 0 ? -0.4 : i * rise, (i + 1) * rise, -hd - 0.6 + ins, hd + 0.6 - ins, SacredPalette.BrickPlinth, BoxFaces.All & ~BoxFaces.Bottom);
                if (c != null)
                {
                    double x, y, z;
                    k.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + i * rise, f.GroundY + (i + 1) * rise, hw + 0.6 - ins, hd + 0.6 - ins, k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Brick);
                }
            }
            if (levels > 0) SacredKit.Stair(m, c, k, -0.8, 0.8, hd + 0.6, plinthTop / Math.Tan(38 * Math.PI / 180), 0, plinthTop, lod, SacredPalette.BrickPlinth, GenColliders.Stone);
            int storeys = Math.Max(1, p.Storeys);
            double total = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + storeys * 2.6 + 0.35 * Math.Max(p.W, p.D);
            double roofRise = Math.Min(0.35 * Math.Max(p.W, p.D), 0.4 * (total - plinthTop));
            double wallTop = total - roofRise;
            double sH = (wallTop - plinthTop) / storeys;
            uint wall = p.WallColour != 0 ? p.WallColour : SacredPalette.BrickDachi;
            MeshKit.Box(m, k, -hw, hw, plinthTop, wallTop, -hd, hd, wall, BoxFaces.All & ~BoxFaces.Bottom);
            SacredKit.SanctumDoor(m, k, 0, plinthTop, hd, 1.0, Math.Min(2.0, sH - 0.3), SacredPalette.WoodCarved, lod);
            KitFrame side = k;
            int windows = 0;
            for (int sd = 0; sd < 4; sd++)
            {
                double half = sd % 2 == 0 ? hd : hw, along = sd % 2 == 0 ? hw : hd;
                int bays = Math.Max(1, Math.Min(5, (int)Math.Round(2 * along / 1.8)));
                for (int s = 1; s < storeys; s++)
                {
                    double v0 = plinthTop + s * sH;
                    if (lod <= 1) MeshKit.Box(m, side, -along, along, v0 - 0.1, v0 + 0.1, half, half + 0.15, SacredPalette.WoodCarved, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                    for (int b = 0; b < bays; b++)
                    {
                        double u = -along + 2 * along * (b + 0.5) / bays;
                        bool centre = sd == 0 && b == bays / 2 && s == storeys - 1;
                        uint pane = centre && p.Figures ? SacredPalette.SanctumDark : MeshColor.Scale(SacredPalette.WoodCarved, 1.3f);
                        MeshKit.Box(m, side, u - 0.5, u + 0.5, v0 + 0.3, v0 + Math.Min(1.5, sH - 0.4), half, half + 0.08, SacredPalette.WoodCarved, lod <= 1 ? BoxFaces.Wall : BoxFaces.Front);
                        MeshKit.Panel(m, side, u - 0.36, v0 + 0.42, u + 0.36, v0 + Math.Min(1.38, sH - 0.52), half + 0.085, pane);
                        windows++;
                        if (centre && p.Figures && lod <= 1)
                        {
                            // Shiva and Parvati looking out of the central upper window.
                            for (int q = -1; q <= 1; q += 2)
                            {
                                double x, y, z;
                                side.ToWorld(u + q * 0.17, v0 + 0.85, half + 0.15, out x, out y, out z);
                                MeshKit.Blob(m, x, y, z, 0.13, 0.2, 0.1, MeshColor.FromHex(0xF4F1E8));
                                MeshKit.Blob(m, x, y + 0.27, z, 0.09, 0.09, 0.09, MeshColor.FromHex(0xF4F1E8));
                            }
                        }
                    }
                }
                side = side.TurnedRight();
            }
            if (p.GiltBalcony)
            {
                double v0 = plinthTop + (storeys - 1) * sH;
                MeshKit.Box(m, k, -0.8 * hw, 0.8 * hw, v0 - 0.15, v0 + 1.2, hd, hd + 0.7, SacredPalette.Gilt, BoxFaces.All & ~BoxFaces.Back);
                MeshKit.Panel(m, k, -0.7 * hw, v0 + 0.1, 0.7 * hw, v0 + 1.05, hd + 0.705, SacredPalette.GiltLo);
            }
            // Hipped tile roof with an overhang and three gilt finials along the ridge.
            uint roof = p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile;
            var rx = new double[4];
            var rz = new double[4];
            double[] us = { -hw - 1.0, hw + 1.0, hw + 1.0, -hw - 1.0 }, ws = { -hd - 1.0, -hd - 1.0, hd + 1.0, hd + 1.0 };
            for (int q = 0; q < 4; q++)
            {
                double y;
                k.ToWorld(us[q], 0, ws[q], out rx[q], out y, out rz[q]);
            }
            if (Polygon.SignedArea(rx, rz, 4) < 0)
            {
                Array.Reverse(rx);
                Array.Reverse(rz);
            }
            double cx, cy, cz;
            k.ToWorld(0, 0, 0, out cx, out cy, out cz);
            MeshKit.Loft(m, rx, rz, 4, cx, cz, f.GroundY + wallTop - 0.4, 1.0, f.GroundY + total, 0.15, roof);
            MeshKit.Loft(m, rx, rz, 4, cx, cz, f.GroundY + wallTop - 0.4, 1.0, f.GroundY + wallTop - 0.4, 0.75, SacredPalette.WoodCarved);
            if (lod <= 1)
            {
                for (int q = -1; q <= 1; q++)
                {
                    double x, y, z;
                    k.ToWorld(q * 0.15 * hw, total, 0, out x, out y, out z);
                    SacredKit.Gajur(m, x, z, y - 0.05, 0.9, 0.18, lod, SacredPalette.Gilt);
                }
            }
            if (p.Lions)
            {
                SacredKit.Guardian(m, k, -1.0, hd + 0.6 + plinthTop / Math.Tan(38 * Math.PI / 180) - 0.3, 0, 1.1, 0, lod);
                SacredKit.Guardian(m, k, 1.0, hd + 0.6 + plinthTop / Math.Tan(38 * Math.PI / 180) - 0.3, 0, 1.1, 0, lod);
            }
            if (c != null) c.AddBox(cx, cz, f.GroundY + plinthTop, f.GroundY + total, hw, hd, k.UX, k.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = (float)total;
                stats.PlinthLevels = levels;
                stats.PlinthTopM = (float)plinthTop;
                stats.DoorYawDeg = f.YawDeg;
                stats.Windows = windows;
            }
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Kal Bhairav relief, Yoganarendra column, vajra
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An open-air stone relief on a platform, no roof (Kal Bhairav: black-blue body, six arms, gilt crown).</summary>
        public static int Relief(float platformW, float platformD, float reliefH, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            MeshKit.Box(m, k, -0.5 * platformW, 0.5 * platformW, -0.3, 0.45, -0.5 * platformD, 0.5 * platformD, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            double slabW = Math.Min(platformW - 0.6, 0.75 * reliefH), back = -0.5 * platformD + 0.5;
            MeshKit.Box(m, k, -0.5 * slabW, 0.5 * slabW, 0.45, 0.45 + reliefH, back - 0.4, back, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            uint body = MeshColor.FromHex(0x1F2A44);
            double x, y, z;
            k.ToWorld(0, 0.45 + 0.42 * reliefH, back + 0.25, out x, out y, out z);
            MeshKit.Blob(m, x, y, z, 0.28 * slabW, 0.3 * reliefH, 0.25, body);
            k.ToWorld(0, 0.45 + 0.8 * reliefH, back + 0.3, out x, out y, out z);
            MeshKit.Blob(m, x, y, z, 0.15 * slabW, 0.12 * reliefH, 0.22, MeshColor.FromHex(0xF2C230));
            MeshKit.Frustum(m, x, z, 0.14 * slabW, y + 0.08 * reliefH, 0.05 * slabW, y + 0.2 * reliefH, 6, true, SacredPalette.Gilt);
            if (lod <= 1)
            {
                for (int a = 0; a < 6; a++)
                {
                    double side = a < 3 ? -1 : 1, lvl = 0.45 + reliefH * (0.35 + 0.12 * (a % 3));
                    double ax, ay, az, bx, by, bz;
                    k.ToWorld(side * 0.15 * slabW, lvl, back + 0.25, out ax, out ay, out az);
                    k.ToWorld(side * 0.45 * slabW, lvl + 0.15 * reliefH * (a % 3 - 1), back + 0.25, out bx, out by, out bz);
                    MeshKit.Bar(m, ax, ay, az, bx, by, bz, 0.12, body);
                }
            }
            if (c != null)
            {
                k.ToWorld(0, 0, 0, out x, out y, out z);
                c.AddBox(x, z, f.GroundY - 0.3, f.GroundY + 0.45, 0.5 * platformW, 0.5 * platformD, k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            }
            if (stats != null)
            {
                stats.TopM = (float)(0.45 + reliefH);
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>A stone pillar with a lotus capital and a kneeling gilt figure under a cobra hood (Yoganarendra
        /// Malla), or with a Garuda when <paramref name="garuda"/>.</summary>
        public static int Column(float heightM, bool garuda, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            MeshKit.Box(m, k, -1.0, 1.0, -0.3, 0.4, -1.0, 1.0, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, -0.6, 0.6, 0.4, 0.8, -0.6, 0.6, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            double shaft = Math.Max(1.0, heightM - 0.8 - 1.45); // plinth 0.8, capital + figure 1.45
            MeshKit.Cylinder(m, f.X, f.Z, 0.3, f.GroundY + 0.8, f.GroundY + 0.8 + shaft, lod <= 1 ? 8 : 4, false, SacredPalette.StoneGrey);
            double top = f.GroundY + 0.8 + shaft;
            MeshKit.Frustum(m, f.X, f.Z, 0.35, top, 0.65, top + 0.35, 8, true, garuda ? SacredPalette.StoneGrey : SacredPalette.Gilt);
            uint fig = garuda ? SacredPalette.StoneGrey : SacredPalette.Gilt;
            MeshKit.Blob(m, f.X, top + 0.75, f.Z, 0.3, 0.4, 0.3, fig);
            MeshKit.Blob(m, f.X, top + 1.25, f.Z, 0.18, 0.18, 0.18, fig);
            if (!garuda && lod <= 1) SacredKit.Torana(m, k, 0, top + 0.9, -0.3, 1.3, SacredPalette.Gilt, lod);
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + heightM, 1.0, 1.0, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            if (stats != null)
            {
                stats.TopM = (float)(top + 1.45 - f.GroundY);
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Swayambhu's great gilt vajra (about 3 m long) on its drum pedestal.</summary>
        public static int Vajra(in GenFrame f, MeshData m)
        {
            int t0 = m.TriangleCount;
            MeshKit.Cylinder(m, f.X, f.Z, 1.4, f.GroundY - 0.2, f.GroundY + 1.1, 12, true, SacredPalette.StoneGrey);
            KitFrame k = f.Kit;
            double ax, ay, az, bx, by, bz;
            k.ToWorld(-1.3, 1.6, 0, out ax, out ay, out az);
            k.ToWorld(1.3, 1.6, 0, out bx, out by, out bz);
            MeshKit.Bar(m, ax, ay, az, bx, by, bz, 0.25, SacredPalette.Gilt);
            MeshKit.Blob(m, ax, ay, az, 0.45, 0.45, 0.45, SacredPalette.Gilt);
            MeshKit.Blob(m, bx, by, bz, 0.45, 0.45, 0.45, SacredPalette.Gilt);
            MeshKit.Blob(m, f.X, f.GroundY + 1.6, f.Z, 0.3, 0.3, 0.3, SacredPalette.GiltHi);
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Towers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A Newar palace tower (the nine-storey Basantapur tower): brick storeys with lattice windows, tiered
        /// sloped roofs on the upper storeys and a top pavilion with a gilt finial.</summary>
        public static int PalaceTower(float w, float d, int storeys, int roofTiers, float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double topRoof = 0.35 * w, sH = (totalM - topRoof) / storeys;
            double hw = 0.5 * w, hd = 0.5 * d;
            int windows = 0;
            for (int s = 0; s < storeys; s++)
            {
                double v0 = s * sH, shrink = s >= storeys - roofTiers ? 1.0 - 0.06 * (s - (storeys - roofTiers)) : 1.0;
                double ux = hw * shrink, wx = hd * shrink;
                MeshKit.Box(m, k, -ux, ux, s == 0 ? -0.3 : v0, v0 + sH, -wx, wx, SacredPalette.BrickDachi, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                if (s >= storeys - roofTiers && s > 0)
                {
                    // A skirt roof at the base of each upper storey.
                    double x, y, z;
                    k.ToWorld(0, 0, 0, out x, out y, out z);
                    var rx = new double[4];
                    var rz = new double[4];
                    double ex = ux + 1.0, ez = wx + 1.0;
                    double[] us = { -ex, ex, ex, -ex }, ws = { -ez, -ez, ez, ez };
                    for (int q = 0; q < 4; q++)
                    {
                        double yy;
                        k.ToWorld(us[q], 0, ws[q], out rx[q], out yy, out rz[q]);
                    }
                    if (Polygon.SignedArea(rx, rz, 4) < 0)
                    {
                        Array.Reverse(rx);
                        Array.Reverse(rz);
                    }
                    MeshKit.Loft(m, rx, rz, 4, x, z, f.GroundY + v0 - 0.4, 1.0, f.GroundY + v0 + 0.5, ux / ex, SacredPalette.Tile);
                }
                if (lod <= 1 && s > 0)
                {
                    KitFrame side = k;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        double half = sd % 2 == 0 ? wx : ux;
                        MeshKit.Panel(m, side, -0.6, v0 + 0.5, 0.6, v0 + Math.Min(sH - 0.4, 1.6), half + 0.01, SacredPalette.WoodCarved);
                        windows++;
                        side = side.TurnedRight();
                    }
                }
            }
            double cx, cy, cz;
            k.ToWorld(0, 0, 0, out cx, out cy, out cz);
            double tw = hw * (1.0 - 0.06 * (roofTiers - 1));
            MeshKit.Frustum(m, cx, cz, (tw + 1.0) * Math.Sqrt(2), f.GroundY + totalM - topRoof - 0.3, 0.0, f.GroundY + totalM - 0.6, 4, false, SacredPalette.Tile);
            SacredKit.Gajur(m, cx, cz, f.GroundY + totalM - 0.8, 0.8, 0.2, lod, SacredPalette.Gilt);
            if (c != null) c.AddBox(cx, cz, f.GroundY, f.GroundY + totalM, hw, hd, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.Tiers = roofTiers;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>The 2021 Dharahara: a 14.2 m base ring, a white fluted shaft of 22 storeys tapering to a 9.3 m top
        /// drum with a balcony ring, a cone cap and a bronze mast, to exactly <paramref name="totalM"/>.</summary>
        public static int Dharahara(float totalM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            double s = totalM / 72.0;
            double baseTop = 1.0 * s, shaftTop = 58.0 * s, drumTop = 63.0 * s, coneTop = 66.0 * s;
            int sides = lod == 0 ? 32 : lod == 1 ? 16 : 8;
            uint white = MeshColor.FromHex(0xF7F6F0);
            MeshKit.Cylinder(m, f.X, f.Z, 7.1, f.GroundY - 0.3, f.GroundY + baseTop, sides, true, white);
            // Fluted shaft: a ring with alternating radii, lofted with a slight taper.
            var x = new double[sides];
            var z = new double[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = 2 * Math.PI * i / sides, r = 5.15 * (i % 2 == 0 ? 1.0 : 0.92);
                x[i] = f.X + r * Math.Cos(a);
                z[i] = f.Z + r * Math.Sin(a);
            }
            MeshKit.Loft(m, x, z, sides, f.X, f.Z, f.GroundY + baseTop, 1.0, f.GroundY + shaftTop, 0.85, white);
            if (lod <= 1)
            {
                KitFrame k = f.Kit;
                int storeys = 22;
                double sh = (shaftTop - baseTop) / storeys;
                for (int q = 1; q < storeys; q++)
                {
                    KitFrame side = k;
                    double rr = 5.15 * (1 - 0.15 * q / storeys) * 0.97;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        MeshKit.Panel(m, side, -0.18, baseTop + q * sh + 0.4, 0.18, baseTop + q * sh + 0.4 + 0.6 * sh, rr, MeshColor.FromHex(0x3A4A5C));
                        side = side.TurnedRight();
                    }
                }
                if (stats != null) stats.Windows = 4 * (storeys - 1);
            }
            // Balcony ring, top drum, cone cap and the bronze mast.
            MeshKit.Frustum(m, f.X, f.Z, 6.2, f.GroundY + shaftTop - 0.3, 6.2, f.GroundY + shaftTop, sides, true, white);
            MeshKit.Frustum(m, f.X, f.Z, 6.2, f.GroundY + shaftTop - 0.3, 4.4, f.GroundY + shaftTop - 0.9, sides, false, white);
            if (lod <= 1) MeshKit.Cylinder(m, f.X, f.Z, 6.15, f.GroundY + shaftTop, f.GroundY + shaftTop + 1.1, sides, false, MeshColor.FromHex(0xC9CED6));
            MeshKit.Cylinder(m, f.X, f.Z, 4.65, f.GroundY + shaftTop, f.GroundY + drumTop, sides, false, white);
            MeshKit.Frustum(m, f.X, f.Z, 5.0, f.GroundY + drumTop, 0.3, f.GroundY + coneTop, sides, false, white);
            MeshKit.Frustum(m, f.X, f.Z, 0.25, f.GroundY + coneTop, 0.05, f.GroundY + totalM, 6, false, MeshColor.FromHex(0x8C6B3A));
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + totalM, 7.1, 7.1, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
            if (stats != null)
            {
                stats.TopM = totalM;
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gates and palace fronts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A palace gate: <paramref name="golden"/> = Bhaktapur's Golden Gate (gilt torana and door in a red
        /// gatehouse set in white walls); else Hanuman Dhoka (gilt door, the Hanuman statue in red cloth under an
        /// umbrella, two lions).</summary>
        public static int Gate(bool golden, float widthM, float heightM, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double hw = 0.5 * widthM + 1.0, depth = 2.5;
            const double RoofH = 1.4;
            heightM = (float)Math.Max(3.0, heightM - RoofH); // the recipe height includes the gatehouse roof
            uint house = golden ? SacredPalette.WoodRed : SacredPalette.BrickDachi;
            MeshKit.Box(m, k, -hw, hw, -0.3, heightM, -depth, 0, house, BoxFaces.All & ~BoxFaces.Bottom);
            double dw = Math.Min(widthM - 0.6, 2.4), dh = Math.Min(heightM - 1.4, 3.2);
            MeshKit.Box(m, k, -0.5 * dw - 0.2, 0.5 * dw + 0.2, 0, dh + 0.2, 0, 0.12, SacredPalette.Gilt, BoxFaces.Wall);
            MeshKit.Panel(m, k, -0.5 * dw, 0, 0.5 * dw, dh, 0.125, golden ? SacredPalette.SanctumDark : SacredPalette.GiltLo);
            SacredKit.Torana(m, k, 0, dh + 0.25, 0.13, dw + 0.8, SacredPalette.Gilt, lod);
            MeshKit.Frustum(m, f.X - k.WX * 1.2, f.Z - k.WZ * 1.2, hw * 1.25, f.GroundY + heightM, 0.3, f.GroundY + heightM + RoofH, 4, false, SacredPalette.Tile);
            if (golden)
            {
                MeshKit.Box(m, k, -hw - 14, -hw, -0.3, heightM - 1.0, -depth + 0.5, -0.3, MeshColor.FromHex(0xF4F2EC), BoxFaces.All & ~BoxFaces.Bottom);
                MeshKit.Box(m, k, hw, hw + 14, -0.3, heightM - 1.0, -depth + 0.5, -0.3, MeshColor.FromHex(0xF4F2EC), BoxFaces.All & ~BoxFaces.Bottom);
            }
            else
            {
                // Hanuman to the left of the gate, under an umbrella; lions either side.
                double x, y, z;
                k.ToWorld(-hw - 1.6, 0, 1.2, out x, out y, out z);
                MeshKit.Box(m, k, -hw - 2.3, -hw - 0.9, 0, 1.0, 0.5, 1.9, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                MeshKit.Blob(m, x, f.GroundY + 1.9, z, 0.45, 0.9, 0.4, SacredPalette.Sindoor);
                MeshKit.Cylinder(m, x, z, 0.04, f.GroundY + 1.0, f.GroundY + 3.6, 4, false, MeshColor.FromHex(0x5A5A5A));
                MeshKit.Frustum(m, x, z, 1.0, f.GroundY + 3.3, 0.0, f.GroundY + 3.8, 8, false, SacredPalette.Sindoor);
                SacredKit.Guardian(m, k, -0.5 * dw - 1.0, 1.0, 0, 1.3, 0, lod);
                SacredKit.Guardian(m, k, 0.5 * dw + 1.0, 1.0, 0, 1.3, 0, lod);
            }
            if (c != null)
            {
                double x, y, z;
                k.ToWorld(-0.5 * hw - 0.5 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + heightM, 0.5 * (hw - 0.5 * dw), 0.5 * depth, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
                k.ToWorld(0.5 * hw + 0.5 * dw, 0, -0.5 * depth, out x, out y, out z);
                c.AddBox(x, z, f.GroundY, f.GroundY + heightM, 0.5 * (hw - 0.5 * dw), 0.5 * depth, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
            if (stats != null)
            {
                stats.TopM = heightM + (float)RoofH;
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>A palace block: <paramref name="rana"/> = a neoclassical Rana front (white stucco, tall French windows,
        /// cornice, pediment and balustrade; Gaddi Baithak); else a Newar palace range with a row of carved windows on the
        /// upper floor (<paramref name="upperWindows"/>: 55 for the 55-Window Palace, 0.9 m pitch).</summary>
        public static int Palace(bool rana, float w, float d, float h, int storeys, int upperWindows, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double hw = 0.5 * w, hd = 0.5 * d;
            double roofRise = rana ? 1.0 : 0.25 * d, wallTop = h - roofRise, sH = wallTop / storeys;
            uint wall = rana ? MeshColor.FromHex(0xF8F5EC) : SacredPalette.BrickDachi;
            MeshKit.Box(m, k, -hw, hw, -0.4, wallTop, -hd, hd, wall, BoxFaces.All & ~BoxFaces.Bottom);
            int windows = 0;
            if (rana)
            {
                int bays = Math.Max(3, (int)Math.Round(w / 3.3));
                for (int s = 0; s < storeys; s++)
                {
                    for (int b = 0; b < bays; b++)
                    {
                        double u = -hw + w * (b + 0.5) / bays;
                        MeshKit.Box(m, k, u - 0.75, u + 0.75, s * sH + 0.6, s * sH + Math.Min(sH - 0.5, 3.2), hd, hd + 0.1, MeshColor.FromHex(0xFFFFFF), lod <= 1 ? BoxFaces.Wall : BoxFaces.Front);
                        MeshKit.Panel(m, k, u - 0.6, s * sH + 0.7, u + 0.6, s * sH + Math.Min(sH - 0.6, 3.1), hd + 0.105, MeshColor.FromHex(0x4E7D52));
                        windows++;
                    }
                    MeshKit.Box(m, k, -hw, hw, (s + 1) * sH - 0.2, (s + 1) * sH, hd, hd + 0.3, MeshColor.FromHex(0xFFFFFF), BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                }
                // Cornice, pediment over the central bays and the balustrade.
                MeshKit.Box(m, k, -hw - 0.4, hw + 0.4, wallTop - 0.4, wallTop, -hd - 0.4, hd + 0.5, MeshColor.FromHex(0xFFFFFF), BoxFaces.All & ~BoxFaces.Bottom);
                MeshKit.TriLocal(m, k, -0.2 * w, wallTop, hd + 0.5, 0.2 * w, wallTop, hd + 0.5, 0, wallTop + 0.12 * w, hd + 0.5, 0, 0, 1, MeshColor.FromHex(0xFFFFFF));
                MeshKit.Box(m, k, -hw, hw, wallTop, h, hd - 0.2, hd + 0.2, MeshColor.FromHex(0xF1E3BE), BoxFaces.Front | BoxFaces.Top | BoxFaces.Back);
            }
            else
            {
                for (int s = 0; s < storeys; s++)
                {
                    int count = s == storeys - 1 && upperWindows > 0 ? upperWindows : Math.Max(1, (int)Math.Round(w / 2.5));
                    double pitch = w / count;
                    for (int b = 0; b < count; b++)
                    {
                        double u = -hw + pitch * (b + 0.5), ww = Math.Min(0.75, 0.8 * pitch);
                        if (lod <= 1) MeshKit.Box(m, k, u - 0.5 * ww - 0.06, u + 0.5 * ww + 0.06, s * sH + 0.5, s * sH + Math.Min(sH - 0.3, 1.6), hd, hd + 0.08, SacredPalette.WoodCarved, BoxFaces.Front | BoxFaces.Top);
                        MeshKit.Panel(m, k, u - 0.5 * ww, s * sH + 0.6, u + 0.5 * ww, s * sH + Math.Min(sH - 0.4, 1.5), hd + 0.085, MeshColor.Scale(SacredPalette.WoodCarved, 1.4f));
                        if (s == storeys - 1) windows++;
                    }
                }
                var rx = new double[4];
                var rz = new double[4];
                double[] us = { -hw - 1.0, hw + 1.0, hw + 1.0, -hw - 1.0 }, ws = { -hd - 1.0, -hd - 1.0, hd + 1.0, hd + 1.0 };
                for (int q = 0; q < 4; q++)
                {
                    double y;
                    k.ToWorld(us[q], 0, ws[q], out rx[q], out y, out rz[q]);
                }
                if (Polygon.SignedArea(rx, rz, 4) < 0)
                {
                    Array.Reverse(rx);
                    Array.Reverse(rz);
                }
                double cx0, cy0, cz0;
                k.ToWorld(0, 0, 0, out cx0, out cy0, out cz0);
                MeshKit.Loft(m, rx, rz, 4, cx0, cz0, f.GroundY + wallTop - 0.3, 1.0, f.GroundY + h, 0.5, SacredPalette.Tile);
            }
            double cx, cy, cz;
            k.ToWorld(0, 0, 0, out cx, out cy, out cz);
            if (c != null) c.AddBox(cx, cz, f.GroundY, f.GroundY + h, hw, hd, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            if (stats != null)
            {
                stats.TopM = h;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>A bell pavilion: four posts on a plinth, a hipped tile roof (to <paramref name="totalM"/>) and the bell.</summary>
        public static int BellPavilion(float w, float d, float totalM, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount;
            float roofRise = (float)Math.Min(2.2, 0.2 * Math.Max(w, d));
            float eaveM = Math.Max(2.4f, totalM - roofRise);
            KitFrame k = f.Kit;
            double hw = 0.5 * w, hd = 0.5 * d;
            MeshKit.Box(m, k, -hw, hw, -0.3, 0.6, -hd, hd, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            for (int q = 0; q < 4; q++)
            {
                double u = (q % 2 == 0 ? -1 : 1) * (hw - 0.5), ww = (q < 2 ? -1 : 1) * (hd - 0.5), x, y, z;
                k.ToWorld(u, 0, ww, out x, out y, out z);
                MeshKit.Cylinder(m, x, z, 0.18, f.GroundY + 0.6, f.GroundY + eaveM, lod <= 1 ? 6 : 4, false, SacredPalette.StoneGrey);
            }
            MeshKit.Frustum(m, f.X, f.Z, Math.Sqrt(hw * hw + hd * hd) + 0.6, f.GroundY + eaveM, 0.2, f.GroundY + totalM, 4, false, SacredPalette.Tile);
            MeshKit.Frustum(m, f.X, f.Z, 0.9, f.GroundY + eaveM - 2.2, 0.45, f.GroundY + eaveM - 0.6, 10, true, MeshColor.FromHex(0x8C6B3A));
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY - 0.3, f.GroundY + 0.6, hw, hd, k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            return m.TriangleCount - t0;
        }

        /// <summary>A hiti (stone water spouts): stage 1 draws the sunken pit as a raised stone rim with a dark floor,
        /// steps on the door side and the makara spouts on the back wall.</summary>
        public static int Hiti(float w, float d, int spouts, bool flowing, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double hw = 0.5 * w, hd = 0.5 * d, rim = 0.5;
            MeshKit.Box(m, k, -hw, hw, -0.2, 0.35, -hd, -hd + rim, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, -hw, -hw + rim, -0.2, 0.35, -hd + rim, hd, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, hw - rim, hw, -0.2, 0.35, -hd + rim, hd, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.QuadLocal(m, k, -hw + rim, 0.02, -hd + rim, hw - rim, 0.02, -hd + rim, hw - rim, 0.02, hd, -hw + rim, 0.02, hd, 0, 1, 0, MeshColor.FromHex(0x3A3F3A));
            int n = Math.Max(1, spouts);
            for (int q = 0; q < n; q++)
            {
                double u = -hw + rim + (w - 2 * rim) * (q + 0.5) / n;
                MeshKit.Box(m, k, u - 0.15, u + 0.15, 0.25, 0.5, -hd + rim, -hd + rim + 0.8, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Back);
                if (flowing && lod <= 1)
                    MeshKit.Panel(m, k.TurnedRight(), -hd + rim + 0.75, 0.02, -hd + rim + 0.8, 0.27, -u, MeshColor.FromHex(0x9CC9E0));
            }
            return m.TriangleCount - t0;
        }

        /// <summary>A bahal or palace courtyard: a quadrangle of ranges around a walkable court, the gate in the middle of
        /// the front range (torana and lions), the court open to the sky. Returns the court's inner half sizes through
        /// <paramref name="innerHalfU"/> and <paramref name="innerHalfW"/>.</summary>
        public static int Courtyard(float w, float d, float rangeDepthM, float heightM, bool lions, uint wall, in GenFrame f, int lod, MeshData m, GenColliders c,
                                    out double innerHalfU, out double innerHalfW)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double hw = 0.5 * w, hd = 0.5 * d, r = Math.Min(rangeDepthM, 0.3 * Math.Min(w, d));
            innerHalfU = hw - r;
            innerHalfW = hd - r;
            double gate = 1.0;
            // Front range split by the gate passage; back and side ranges whole.
            Range(m, k, -hw, -gate, hd - r, hd, heightM, wall, c);
            Range(m, k, gate, hw, hd - r, hd, heightM, wall, c);
            Range(m, k, -hw, hw, -hd, -hd + r, heightM, wall, c);
            Range(m, k, -hw, -hw + r, -hd + r, hd - r, heightM, wall, c);
            Range(m, k, hw - r, hw, -hd + r, hd - r, heightM, wall, c);
            MeshKit.Box(m, k, -gate, gate, 2.0, heightM, hd - r, hd, wall, BoxFaces.All & ~BoxFaces.Bottom);
            SacredKit.Torana(m, k, 0, 2.0, hd + 0.01, 2.4, SacredPalette.Gilt, lod);
            if (lions)
            {
                SacredKit.Guardian(m, k, -gate - 0.7, hd + 0.7, 0, 1.1, 0, lod);
                SacredKit.Guardian(m, k, gate + 0.7, hd + 0.7, 0, 1.1, 0, lod);
            }
            // Court paving (walkable).
            MeshKit.QuadLocal(m, k, -innerHalfU, 0.05, -innerHalfW, innerHalfU, 0.05, -innerHalfW, innerHalfU, 0.05, innerHalfW, -innerHalfU, 0.05, innerHalfW,
                              0, 1, 0, SacredPalette.StoneGrey);
            if (c != null)
            {
                double x, y, z;
                k.ToWorld(0, 0, 0, out x, out y, out z);
                c.AddBox(x, z, f.GroundY - 0.3, f.GroundY + 0.05, innerHalfU, innerHalfW, k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            }
            return m.TriangleCount - t0;
        }

        private static void Range(MeshData m, in KitFrame k, double u0, double u1, double w0, double w1, double h, uint wall, GenColliders c)
        {
            MeshKit.Box(m, k, u0, u1, -0.3, h, w0, w1, wall, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, u0 - 0.4, u1 + 0.4, h, h + 0.15, w0 - 0.4, w1 + 0.4, SacredPalette.Tile, BoxFaces.All);
            if (c != null)
            {
                double x, y, z;
                k.ToWorld(0.5 * (u0 + u1), 0, 0.5 * (w0 + w1), out x, out y, out z);
                c.AddBox(x, z, y - 0.3, y + h, 0.5 * (u1 - u0), 0.5 * (w1 - w0), k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }
    }
}
