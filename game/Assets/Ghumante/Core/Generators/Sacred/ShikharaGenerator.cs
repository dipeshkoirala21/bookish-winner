using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// Parameters of a shikhara (W2_DESIGN 3.2; temples.md 2.2). <see cref="Stone"/>: the granthakuta of carved stone
    /// (Krishna Mandir, Vatsala Durga): a stepped plinth, an arcaded ground floor, rings of pavilions (chhatris) around a
    /// curvilinear central tower, amalaka, kalasha and gilt finial; the pinnacle count is the pavilions plus the tower
    /// (Krishna Mandir: 12 + 8 + 1 = 21). Otherwise the plastered tower (Anantapur, Pratappur): ratha offsets on a
    /// stepped base, white or brick, with a pennant.
    /// </summary>
    public struct ShikharaParams
    {
        public float PlinthW, PlinthD;
        public int PlinthLevels;
        public float StepRiseM;
        public bool Stone;
        public float TowerBaseFrac, TowerHeightFrac;

        /// <summary>Arcaded ground floor height (stone) or sanctum body height (plastered), 0 = derive.</summary>
        public float GroundStoreyM;

        public int PavilionsStorey1, PavilionsStorey2;
        public int Rathas;
        public float TotalHeightM;
        public uint Colour;
        public bool Pennant;
        public double[] OutlineX, OutlineZ;

        /// <summary>Stone lions on the stair (Krishna Mandir).</summary>
        public bool Lions;

        /// <summary>A hero replica (richer figures).</summary>
        public bool Hero;

        /// <summary>Spacing multiplier of the repeated detail (≥ 1; 0 = 1), raised by <see cref="SacredSelector.Thin"/>
        /// so a large generic structure keeps its LOD0 form within the generic ceiling.</summary>
        public float DetailScale;

        public static ShikharaParams StoneDefaults(float w, float d)
        {
            return new ShikharaParams
            {
                PlinthW = w, PlinthD = d, PlinthLevels = 3, StepRiseM = 0.33f, Stone = true, TowerBaseFrac = 0.38f, TowerHeightFrac = 2.2f,
                PavilionsStorey1 = 8, PavilionsStorey2 = 4, Rathas = 12, Colour = MeshColor.FromHex(0x9A8B76), Lions = true,
            };
        }

        public static ShikharaParams PlasterDefaults(float w, float d)
        {
            return new ShikharaParams
            {
                PlinthW = w, PlinthD = d, PlinthLevels = 2, StepRiseM = 0.4f, Stone = false, TowerBaseFrac = 0.8f, TowerHeightFrac = 2.2f,
                Rathas = 16, Colour = MeshColor.FromHex(0xF4F0E6), Pennant = true,
            };
        }
    }

    /// <summary>
    /// The shikhara generator (W2_DESIGN 3.2; ref_temples 3). Stone granthakuta (Krishna Mandir, Vatsala Durga): a
    /// moulded three-step plinth with stone lions at the stair, an arcaded ground floor of cusped arches on slim pillars
    /// in front of a dark corridor, a frieze beam and a balustraded balcony, an arcaded first storey, pavilions
    /// (chhatris: four pillars, a ribbed curved cap and a gilt finial) at the corners and along the sides of both upper
    /// storeys, and the tall central spire with ratha offsets and horizontal ribs, an amalaka, kalasha and gilt finial.
    /// Plastered (Anantapur, Pratappur): a stepped base, a cella with an arched door on every side, the white ribbed
    /// curvilinear spire with four corner turrets, amalaka, gilt finial and a pennant.
    /// </summary>
    public static class ShikharaGenerator
    {
        [ThreadStatic] private static Profile2 _prof;

        private static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(32))).Clear(false); }
        }

        /// <summary>Pinnacles a parameter set produces: the pavilions plus the central tower.</summary>
        public static int Pinnacles(in ShikharaParams p)
        {
            return p.Stone ? p.PavilionsStorey1 + p.PavilionsStorey2 + 1 : 1;
        }

        public static int Build(in ShikharaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in ShikharaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double w = p.PlinthW, d = p.PlinthD > 0 ? p.PlinthD : p.PlinthW, a = d / w;
            int levels = Math.Max(0, p.PlinthLevels);
            double plinthTop = levels * p.StepRiseM;
            uint col = p.Colour != 0 ? p.Colour : p.Stone ? SacredPalette.StoneBuff : SacredPalette.Whitewash;
            ShapeBrush body = Looks.Of(col, p.Stone ? MaterialChannel.Stone : MaterialChannel.Plaster);
            ShapeBrush trim = Looks.Of(MeshColor.Scale(col, p.Stone ? 0.86f : 0.94f), p.Stone ? MaterialChannel.Stone : MaterialChannel.Plaster);
            ShapeBrush plinthB = p.Stone ? Looks.Of(MeshColor.Scale(col, 0.9f), MaterialChannel.Stone) : Looks.Of(MeshColor.FromHex(0xC9C2B4), MaterialChannel.Stone);
            double cx, cy, cz;
            kf.ToWorld(0, 0, 0, out cx, out cy, out cz);
            if (lod >= 3)
            {
                double tot = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + 4 + p.TowerHeightFrac * p.TowerBaseFrac * w;
                double gt = plinthTop + Math.Min(4.0, 0.25 * tot);
                SacredDraw.Box(m, k, -0.5 * w, 0.5 * w, -0.3, plinthTop, -0.5 * d, 0.5 * d, plinthB, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, k, -0.4 * w, 0.4 * w, plinthTop, gt, -0.4 * d, 0.4 * d, body, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Cone(m, SacredDraw.At(k, 0, gt, 0), body, 0.4 * Math.Sqrt(w * w + d * d), tot - gt, 4, 0, 0, false);
                if (stats != null)
                {
                    stats.Pinnacles = Pinnacles(p);
                    stats.PlinthLevels = levels;
                    stats.TopM = (float)tot;
                    stats.DoorYawDeg = f.YawDeg;
                }
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }

            // 1. Plinth with the stair on the door side.
            double inset = Math.Min(0.06 * w, 0.4);
            for (int i = 0; i < levels; i++)
            {
                double hw = 0.5 * w - i * inset;
                if (i == 0 && p.OutlineX != null && p.OutlineX.Length >= 3)
                {
                    int n = p.OutlineX.Length;
                    double[] ou = new double[n], ow = new double[n];
                    for (int q = 0; q < n; q++) Local(kf, p.OutlineX[q], p.OutlineZ[q], out ou[q], out ow[q]);
                    SacredParts.PlinthLevel(m, k, ou, ow, n, 0, p.StepRiseM, 0.4, plinthB, trim, lod);
                }
                else
                {
                    SacredParts.PlinthLevel(m, k, hw, hw * a, i * p.StepRiseM, (i + 1) * p.StepRiseM, i == 0 ? 0.4 : 0.05, plinthB, trim, lod);
                }
                if (c != null) c.AddBox(cx, cz, f.GroundY + i * p.StepRiseM, f.GroundY + (i + 1) * p.StepRiseM, hw, hw * a, kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            }
            int steps = 0;
            if (levels > 0)
            {
                double stairW = Math.Max(1.2, 0.25 * w), run = plinthTop / Math.Tan(36 * Math.PI / 180);
                steps = SacredParts.Stair(m, c, kf, k, 0.5 * stairW, 0.5 * d, run, 0, plinthTop, 0, 0, plinthB, plinthB, lod, GenColliders.Stone);
                if (p.Lions && lod <= 2)
                {
                    double gh = Math.Min(1.5, 0.1 * w + 0.3);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        SacredParts.Pedestal(m, k, s * (0.5 * stairW + 0.4), 0, 0.5 * d + 0.5 * run, 0.35, 0.4, plinthTop + 0.1, plinthB, lod);
                        SacredFigures.Guardian(m, k, s * (0.5 * stairW + 0.4), plinthTop + 0.1, 0.5 * d + 0.5 * run, gh, GuardianKind.Lion, false, p.Hero ? SacredFigures.Hero : SacredFigures.Generic, lod, s);
                    }
                }
            }
            double bodyHW = 0.5 * w - levels * inset;
            double total = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + 4 + p.TowerHeightFrac * p.TowerBaseFrac * w;
            int pinnacles = 1;
            double sanctumHalf, gTop;
            if (p.Stone)
                pinnacles = StoneBody(p, kf, k, f, bodyHW, a, plinthTop, total, body, trim, lod, m, c, out sanctumHalf, out gTop);
            else
                PlasterBody(p, kf, k, f, bodyHW, a, plinthTop, total, body, trim, lod, m, c, out sanctumHalf, out gTop);
            if (stats != null)
            {
                stats.Pinnacles = pinnacles;
                stats.PlinthLevels = levels;
                stats.PlinthTopM = (float)plinthTop;
                stats.TopM = (float)total;
                stats.DoorYawDeg = f.YawDeg;
                stats.Steps = steps;
                stats.SanctumHalfU = (float)sanctumHalf;
                stats.SanctumHalfW = (float)(sanctumHalf * a);
                stats.SanctumV0 = (float)plinthTop;
                stats.SanctumV1 = (float)gTop;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        private static void Local(in KitFrame k, double x, double z, out double u, out double w)
        {
            double dx = x - k.OX, dz = z - k.OZ;
            u = dx * k.UX + dz * k.UZ;
            w = dx * k.UZ - dz * k.UX;
        }

        /// <summary>Krishna Mandir type: arcaded ground floor, balcony, arcaded first storey, two rings of pavilions and the
        /// central spire. Returns the pinnacles.</summary>
        private static int StoneBody(in ShikharaParams p, in KitFrame kf, in Affine3 k, in GenFrame f, double bodyHW, double a, double plinthTop, double total,
                                     in ShapeBrush body, in ShapeBrush trim, int lod, MeshData m, GenColliders c, out double sanctumHalf, out double gTop)
        {
            double gH = p.GroundStoreyM > 0 ? p.GroundStoreyM : Math.Min(4.2, 0.21 * total);
            gTop = plinthTop + gH;
            double corridor = Math.Min(1.8, 0.14 * bodyHW * 2);
            sanctumHalf = bodyHW - corridor - 0.3;
            // The inner sanctum block (dark corridor walls behind the arcade) and its closed door.
            ShapeBrush inner = Looks.Of(MeshColor.Scale(body.Color, 0.62f), MaterialChannel.Stone, 0.6f);
            SacredDraw.Box(m, k, -sanctumHalf, sanctumHalf, plinthTop, gTop, -sanctumHalf * a, sanctumHalf * a, inner, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            SacredParts.Door(m, k, 0, plinthTop, sanctumHalf * a, Math.Min(1.1, 0.3 * sanctumHalf), Math.Min(2.1, gH - 0.9), Looks.WoodDark, lod);
            SacredDraw.CapRect(m, k, 0, 0, bodyHW, bodyHW * a, gTop - 0.55, false, Looks.Ao(body, 0.55f));
            int bays = Math.Max(3, Math.Min(9, (int)Math.Round(2 * bodyHW / (1.8 * Math.Max(1.0, p.DetailScale)))));
            if (bays % 2 == 0) bays++;
            int arcLod = p.DetailScale >= 1.6f ? Math.Max(1, lod) : lod; // a large generic tower: coarser arches, same form
            Arcade(m, k, bodyHW, bodyHW * a, plinthTop, gTop - 0.55, bays, body, arcLod);
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + plinthTop, f.GroundY + gTop, sanctumHalf, sanctumHalf * a, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);

            // Frieze beam and the balcony with its balustrade.
            Mould fb = SacredDraw.M;
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, bodyHW, bodyHW * a, pu, pw);
            fb.Add(0, gTop - 0.55, trim).Add(0.06, gTop - 0.5).Add(0.06, gTop - 0.12, Looks.Of(MeshColor.Scale(body.Color, 0.92f), MaterialChannel.Stone)).Add(0.18, gTop - 0.12, trim)
              .Add(0.32, gTop, trim);
            SacredDraw.Ring(m, k, pu, pw, n, fb);
            SacredDraw.Cap(m, k, pu, pw, n, 0.32, gTop, true, body);
            double bal = 0.95;
            if (lod <= 1)
            {
                Mould br = SacredDraw.M;
                br.Add(0.2, gTop, body).Add(0.2, gTop + 0.12).Add(0.14, gTop + 0.12, trim).Add(0.14, gTop + bal - 0.12).Add(0.24, gTop + bal - 0.1, body).Add(0.24, gTop + bal)
                  .Add(0.02, gTop + bal).Add(0.02, gTop);
                SacredDraw.Ring(m, k, pu, pw, n, br);
            }

            // Storey 1: arcaded core with pavilions round it.
            double tower = p.TowerBaseFrac * bodyHW * 2;
            double s1Half = Math.Max(0.5 * tower + 0.3, 0.72 * bodyHW), s1H = Math.Min(3.4, 0.16 * total), s1Top = gTop + s1H;
            SacredDraw.Box(m, k, -s1Half + 0.6, s1Half - 0.6, gTop, s1Top, -(s1Half - 0.6) * a, (s1Half - 0.6) * a, inner, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Arcade(m, k, s1Half, s1Half * a, gTop, s1Top - 0.4, 3, body, arcLod);
            int n1 = SacredDraw.Rect(0, 0, s1Half, s1Half * a, pu, pw);
            Mould c1 = SacredDraw.M;
            c1.Add(0, s1Top - 0.4, trim).Add(0.12, s1Top - 0.25).Add(0.25, s1Top);
            SacredDraw.Ring(m, k, pu, pw, n1, c1);
            SacredDraw.Cap(m, k, pu, pw, n1, 0.25, s1Top, true, body);
            double pav = Math.Max(1.0, 0.16 * 2 * bodyHW);
            int pin = 1;
            bool thin = p.DetailScale >= 1.6f;
            pin += Pavilions(m, k, thin ? Math.Min(4, p.PavilionsStorey1) : p.PavilionsStorey1, bodyHW - 0.55 * pav, gTop, pav, Math.Min(3.6, 0.2 * total), body, trim, lod);

            // Storey 2: smaller core and pavilions, then the spire.
            double s2Half = Math.Max(0.5 * tower + 0.2, 0.62 * s1Half), s2H = Math.Min(2.8, 0.13 * total), s2Top = s1Top + s2H;
            SacredDraw.Box(m, k, -s2Half, s2Half, s1Top, s2Top, -s2Half * a, s2Half * a, body, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            if (lod <= 1)
            {
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                    double half = sd % 2 == 0 ? s2Half * a : s2Half;
                    SacredDraw.Panel(m, side, -0.35 * s2Half, s1Top + 0.3, 0.35 * s2Half, s2Top - 0.4, half + 0.01, inner);
                    SacredParts.Torana(m, side, 0, s2Top - 0.4, half + 0.01, 0.42 * s2Half, Looks.Ao(Looks.Gilt, 0.95f), lod + 1);
                }
            }
            pin += Pavilions(m, k, thin ? 0 : p.PavilionsStorey2, s1Half - 0.5 * 0.8 * pav, s1Top, 0.8 * pav, Math.Min(3.0, 0.16 * total), body, trim, lod);
            double finialH = 0.07 * total, towerTop = total - finialH;
            double topHalf = Spire(m, k, 0.5 * tower, s2Top - 0.1, towerTop - s2Top + 0.1, body, trim, p.Rathas, lod);
            Crown(m, k, topHalf, towerTop, total, body, lod);
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + gTop, f.GroundY + total, 0.5 * tower, 0.5 * tower, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            return pin;
        }

        /// <summary>Anantapur type: white cella with arched doors, corner turrets, the ribbed spire and a pennant.</summary>
        private static void PlasterBody(in ShikharaParams p, in KitFrame kf, in Affine3 k, in GenFrame f, double bodyHW, double a, double plinthTop, double total,
                                        in ShapeBrush body, in ShapeBrush trim, int lod, MeshData m, GenColliders c, out double sanctumHalf, out double gTop)
        {
            double gH = p.GroundStoreyM > 0 ? p.GroundStoreyM : Math.Min(0.3 * total, 0.6 * 2 * bodyHW);
            gTop = plinthTop + gH;
            sanctumHalf = bodyHW;
            SacredParts.BandedWall(m, k, bodyHW, bodyHW * a, plinthTop, gTop, body, trim, lod, true, true);
            double dw = Math.Min(1.1, 0.28 * bodyHW), dh = Math.Min(2.0, gH - 0.7);
            for (int sd = 0; sd < 4; sd++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                double half = sd % 2 == 0 ? bodyHW * a : bodyHW;
                if (sd == 0) SacredParts.Door(m, side, 0, plinthTop, half, dw, dh, Looks.WoodDark, lod, false);
                else SacredDraw.Panel(m, side, -0.5 * dw, plinthTop + 0.2, 0.5 * dw, plinthTop + dh, half + 0.01, Looks.Recess);
                if (lod <= 1) Arch(m, side, 0, plinthTop + dh, half + 0.02, dw + 0.3, 0.5 * dw + 0.25, 0.14, 0.08, true, trim, lod);
            }
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + plinthTop, f.GroundY + gTop, bodyHW, bodyHW * a, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
            double finialH = 0.07 * total, towerTop = total - finialH;
            double tower = Math.Min(2 * bodyHW, p.TowerBaseFrac * bodyHW * 2);
            // Corner turrets at the spire's foot.
            if (lod <= 1)
            {
                double th = 0.22 * (towerTop - gTop), tr = 0.16 * tower;
                for (int q = 0; q < 4; q++)
                {
                    double tu = (q % 2 == 0 ? 1 : -1) * (bodyHW - tr), tw = (q < 2 ? 1 : -1) * (bodyHW * a - tr);
                    Spire(m, SacredDraw.At(k, tu, 0, tw), tr, gTop, th, body, trim, 8, lod + 1);
                }
            }
            double topHalf = Spire(m, k, 0.5 * tower, gTop - 0.05, towerTop - gTop + 0.05, body, trim, p.Rathas, lod);
            Crown(m, k, topHalf, towerTop, total, body, lod);
            if (p.Pennant && lod <= 2)
            {
                double pv = total - 0.45 * finialH;
                Shapes.Cylinder(m, SacredDraw.At(k, 0.5 * topHalf, towerTop, 0), Looks.Bronze, 0.03, 0.4 * finialH + 1.4, 4);
                SacredDraw.Tri(m, k, 0.5 * topHalf, pv + 1.3, 0, 0.5 * topHalf, pv + 0.5, 0, 0.5 * topHalf + 1.6, pv + 0.85, 0.2, 0, 0, 1, Looks.Of(SacredPalette.Sindoor, MaterialChannel.Fabric));
                SacredDraw.Tri(m, k, 0.5 * topHalf, pv + 1.3, 0, 0.5 * topHalf, pv + 0.5, 0, 0.5 * topHalf + 1.6, pv + 0.85, 0.2, 0, 0, -1, Looks.Of(SacredPalette.Sindoor, MaterialChannel.Fabric));
            }
            if (c != null) c.AddBox(kf.OX, kf.OZ, f.GroundY + gTop, f.GroundY + total, 0.5 * tower, 0.5 * tower, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        /// <summary>
        /// A curvilinear spire (rekha shikhara) from v0 rising h: a ratha plan (a square whose sides step out twice to
        /// the middle) narrowing on the classic Nagara curve to about 0.32 × its base, with horizontal ribs (bhumi
        /// amalakas) every few rows. Returns the half size at the top.
        /// </summary>
        public static double Spire(MeshData m, in Affine3 k, double half, double v0, double h, in ShapeBrush body, in ShapeBrush rib, int rathas, int lod)
        {
            double[] pu = ShapeScratch.U, pw = ShapeScratch.W;
            int n = lod >= 2 ? SacredDraw.Rect(0, 0, half, half, pu, pw) : StupaGenerator.TwentyCornerLocal(half, pu, pw);
            if (lod <= 1 && rathas < 14)
            {
                n = SacredDraw.Rect(0, 0, half, half, pu, pw);
                // 12-corner cross for the smaller spires.
                double s = 0.4 * half, dd = 0.08 * half;
                double[] ru = { half, s, s, -s, -s, -half, -half - dd, -half - dd, -half, -s, -s, s, s, half, half + dd, half + dd };
                double[] rw = { half, half, half + dd, half + dd, half, half, s, -s, -half, -half, -half - dd, -half - dd, -half, -half, -s, s };
                n = 16;
                for (int i = 0; i < n; i++)
                {
                    pu[i] = ru[i];
                    pw[i] = rw[i];
                }
                if (SacredDraw.Area(pu, pw, n) < 0)
                {
                    Array.Reverse(pu, 0, n);
                    Array.Reverse(pw, 0, n);
                }
            }
            int rows = lod == 0 ? 16 : lod == 1 ? 9 : 4;
            double topScale = 0.3;
            double sign = SacredDraw.Area(pu, pw, n) >= 0 ? 1 : -1;
            for (int e = 0; e < n; e++)
            {
                int e1 = (e + 1) % n;
                double du = pu[e1] - pu[e], dw = pw[e1] - pw[e], dl = Math.Sqrt(du * du + dw * dw);
                if (dl < 1e-9) continue;
                double nu = sign * dw / dl, nw = -sign * du / dl;
                int first = m.VertexCount;
                for (int r = 0; r <= rows; r++)
                {
                    double t = (double)r / rows;
                    double sc = Scale(t, topScale), y = v0 + h * t;
                    bool ribRow = lod == 0 && r > 0 && r < rows && r % 4 == 0;
                    double bump = ribRow ? 1.04 : 1.0;
                    // Slope of the curve gives the normal's vertical part.
                    double ds = (Scale(Math.Min(1, t + 0.01), topScale) - Scale(Math.Max(0, t - 0.01), topScale)) / 0.02;
                    double hd = -ds * Math.Sqrt(pu[e] * pu[e] + pw[e] * pw[e]) / h, ny = Math.Max(0, hd);
                    ShapeBrush b = ribRow ? rib : body;
                    SacredDraw.V(m, k, pu[e] * sc * bump, y, pw[e] * sc * bump, nu, ny, nw, b);
                    SacredDraw.V(m, k, pu[e1] * sc * bump, y, pw[e1] * sc * bump, nu, ny, nw, b);
                }
                // Rows of two vertices: connect as a strip.
                for (int r = 0; r < rows; r++)
                {
                    int q = first + 2 * r;
                    SacredDraw.Quad(m, q, q + 1, q + 3, q + 2);
                }
            }
            return half * topScale;
        }

        private static double Scale(double t, double top)
        {
            // Bulging Nagara curve: nearly vertical at the foot, closing in hard toward the top (the bullet-shaped spires of
            // Vatsala Durga and Krishna Mandir, refs vatsala 00, krishna 02).
            return 1 - (1 - top) * Math.Pow(t, 2.5);
        }

        /// <summary>The spire's crown: a ribbed amalaka disc, the kalasha and a gilt finial up to <paramref name="total"/>.</summary>
        private static void Crown(MeshData m, in Affine3 k, double topHalf, double v0, double total, in ShapeBrush body, int lod)
        {
            double r = 1.25 * topHalf, h = 0.55 * topHalf;
            Profile2 p = Prof;
            p.Add(0, v0 - 0.05).Add(0.85 * topHalf, v0 - 0.05, true).Add(0.8 * topHalf, v0 + 0.08).Add(r, v0 + 0.35 * h).Add(r * 1.03, v0 + 0.55 * h)
             .Add(r, v0 + 0.75 * h).Add(0.6 * topHalf, v0 + h, true).Add(0.4 * topHalf, v0 + h + 0.05).Add(0, v0 + h + 0.05);
            Shapes.Lathe(m, k, Looks.Of(MeshColor.Scale(body.Color, 0.95f), body.Channel), p, lod == 0 ? 16 : 8);
            double g0 = v0 + h;
            SacredParts.Gajur(m, k, g0, Math.Max(0.4, total - g0), Math.Max(0.15, 0.45 * topHalf), Looks.Gilt, lod);
        }

        /// <summary>
        /// An arcade round a rectangle of half sizes (hu, hw) from v0 to v1: per side <paramref name="bays"/> cusped arches
        /// on slim pillars, spandrels up to a beam, and the dark corridor behind (the caller's inner block).
        /// </summary>
        private static void Arcade(MeshData m, in Affine3 k, double hu, double hw, double v0, double v1, int bays, in ShapeBrush body, int lod)
        {
            double h = v1 - v0, pt = Math.Min(0.34, 0.4 * h / bays + 0.12);
            for (int sd = 0; sd < 4; sd++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                double along = sd % 2 == 0 ? hu : hw, wall = sd % 2 == 0 ? hw : hu;
                double bay = 2 * along / bays;
                double spring = v1 - 0.32 * bay - 0.05, rise = Math.Min(0.5 * bay, v1 - spring - 0.1);
                for (int b = 0; b <= bays; b++)
                {
                    double x = -along + b * bay;
                    if (lod >= 2) SacredDraw.Box(m, side, x - 0.5 * pt, x + 0.5 * pt, v0, v1, wall - pt, wall, body, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    else SacredParts.Post(m, side, x, wall - 0.5 * pt, v0, spring, pt, body, lod);
                }
                if (lod >= 2) continue;
                for (int b = 0; b < bays; b++)
                {
                    double x = -along + (b + 0.5) * bay;
                    Arch(m, side, x, spring, wall, bay - pt, rise, 0.12, 0.3, lod == 0, body, lod);
                }
                // Beam over the arches.
                SacredDraw.Box(m, side, -along - 0.5 * pt, along + 0.5 * pt, v1 - 0.04, v1, wall - 0.3, wall + 0.04, body, BoxFaces.Front | BoxFaces.Bottom);
            }
        }

        /// <summary>
        /// An arch on the wall plane w facing +w: a ring of thickness th (cusped into lobes when <paramref name="lobed"/>)
        /// springing at v over a span, with its soffit running back <paramref name="depth"/>, and spandrels filling up to
        /// v + rise + th as a flat wall.
        /// </summary>
        public static void Arch(MeshData m, in Affine3 k, double u, double v, double w, double span, double rise, double th, double depth, bool lobed, in ShapeBrush b,
                                int lod)
        {
            int seg = lod == 0 ? 12 : 6;
            double r = 0.5 * span;
            int f0 = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double t = Math.PI * i / seg, lobe = lobed ? 1 - 0.1 * Math.Abs(Math.Sin(3 * t)) : 1;
                double cxp = Math.Cos(t), syp = Math.Sin(t);
                double iu = u + r * cxp * lobe, iv = v + rise * syp * lobe;
                double ou = u + (r + th) * cxp, ov = v + (rise + th) * syp;
                // Front face: inner and outer, then the spandrel up to the top line.
                SacredDraw.V(m, k, iu, iv, w + 0.03, 0, 0, 1, b);
                SacredDraw.V(m, k, ou, ov, w + 0.03, 0, 0, 1, b);
                SacredDraw.V(m, k, ou, ov, w, 0, 0, 1, b);
                SacredDraw.V(m, k, ou, v + rise + th, w, 0, 0, 1, b);
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = f0 + 4 * i, a1 = a0 + 4;
                SacredDraw.Quad(m, a0, a1, a1 + 1, a0 + 1);
                SacredDraw.Quad(m, a0 + 2, a1 + 2, a1 + 3, a0 + 3);
            }
            // Soffit: the underside of the ring running back into the wall.
            int s0 = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double t = Math.PI * i / seg, lobe = lobed ? 1 - 0.1 * Math.Abs(Math.Sin(3 * t)) : 1;
                double cxp = Math.Cos(t), syp = Math.Sin(t);
                double iu = u + r * cxp * lobe, iv = v + rise * syp * lobe;
                SacredDraw.V(m, k, iu, iv, w + 0.03, -cxp, -syp, 0, Looks.Ao(b, 0.75f));
                SacredDraw.V(m, k, iu, iv, w - depth, -cxp, -syp, 0, Looks.Ao(b, 0.55f));
            }
            SacredDraw.Grid(m, s0, seg + 1, 2, false, null);
        }

        /// <summary>n pavilions (chhatris) on a square ring of half size <paramref name="half"/> standing at v: 4 = corners,
        /// 8 = corners and mid-sides, 12 = corners and two per side; each a slab, four pillars, an entablature, a ribbed
        /// curved cap and a gilt finial. Returns the pavilions drawn.</summary>
        private static int Pavilions(MeshData m, in Affine3 k, int n, double half, double v, double size, double height, in ShapeBrush body, in ShapeBrush trim, int lod)
        {
            if (n <= 0) return 0;
            int count = 0, perSide = Math.Max(0, (n - 4) / 4);
            for (int i = 0; i < n; i++)
            {
                double pu, pw;
                if (i < 4)
                {
                    pu = (i % 2 == 0 ? 1 : -1) * half;
                    pw = (i < 2 ? 1 : -1) * half;
                }
                else
                {
                    int j = i - 4, sd = (j / Math.Max(1, perSide)) % 4, q = j % Math.Max(1, perSide);
                    double t = perSide == 1 ? 0 : -0.5 + (double)q / (perSide - 1);
                    SacredParts.OnRect(sd, t, half, half, out pu, out pw);
                }
                Pavilion(m, SacredDraw.At(k, pu, v, pw), size, height, body, trim, lod);
                count++;
            }
            return count;
        }

        private static void Pavilion(MeshData m, in Affine3 f, double size, double height, in ShapeBrush body, in ShapeBrush trim, int lod)
        {
            double hs = 0.5 * size, ph = 0.5 * height, pt = Math.Max(0.12, 0.12 * size);
            if (lod >= 2)
            {
                SacredDraw.Box(m, f, -hs, hs, 0, ph, -hs, hs, body, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Cone(m, SacredDraw.At(f, 0, ph, 0), body, hs * 1.1, height - ph, 4, 0, 0, false);
                return;
            }
            SacredDraw.Box(m, f, -hs - 0.05, hs + 0.05, 0, 0.12, -hs - 0.05, hs + 0.05, trim, BoxFaces.All & ~BoxFaces.Bottom);
            for (int q = 0; q < 4; q++)
            {
                double u = (q % 2 == 0 ? 1 : -1) * (hs - 0.5 * pt), w = (q < 2 ? 1 : -1) * (hs - 0.5 * pt);
                SacredDraw.Box(m, f, u - 0.5 * pt, u + 0.5 * pt, 0.12, ph, w - 0.5 * pt, w + 0.5 * pt, body, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            }
            SacredDraw.Box(m, f, -hs - 0.06, hs + 0.06, ph, ph + 0.18, -hs - 0.06, hs + 0.06, trim, BoxFaces.All);
            // A ribbed curved cap (a squarish bell) and the gilt finial.
            Profile2 p = Prof;
            double ch = height - ph - 0.18 - 0.35;
            p.Add(0, 0).Add(hs * 1.08, 0, true).Add(hs * 1.02, 0.25 * ch).Add(hs * 0.8, 0.55 * ch).Add(hs * 0.45, 0.85 * ch).Add(hs * 0.2, ch, true).Add(0, ch);
            Shapes.Lathe(m, SacredDraw.At(f, 0, ph + 0.18, 0), body, p, lod == 0 ? 8 : 4, default, 45);
            Profile2 g = Prof;
            g.Add(0, 0).Add(0.09, 0, true).Add(0.12, 0.08).Add(0.06, 0.18).Add(0.08, 0.24).Add(0.02, 0.35).Add(0, 0.35);
            Shapes.Lathe(m, SacredDraw.At(f, 0, height - 0.35, 0), Looks.Gilt, g, lod == 0 ? 6 : 4);
        }
    }
}
