using System;
using Ghumante.Core.Meshing;

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

        public static ShikharaParams StoneDefaults(float w, float d)
        {
            return new ShikharaParams
            {
                PlinthW = w, PlinthD = d, PlinthLevels = 3, StepRiseM = 0.33f, Stone = true, TowerBaseFrac = 0.5f, TowerHeightFrac = 2.2f,
                PavilionsStorey1 = 8, PavilionsStorey2 = 4, Rathas = 12, Colour = SacredPalette.StoneBuff,
            };
        }

        public static ShikharaParams PlasterDefaults(float w, float d)
        {
            return new ShikharaParams
            {
                PlinthW = w, PlinthD = d, PlinthLevels = 2, StepRiseM = 0.4f, Stone = false, TowerBaseFrac = 0.8f, TowerHeightFrac = 2.2f,
                Rathas = 16, Colour = SacredPalette.Whitewash, Pennant = true,
            };
        }
    }

    /// <summary>The shikhara generator (stone granthakuta and plastered tower; W2_DESIGN 3.2).</summary>
    public static class ShikharaGenerator
    {
        [ThreadStatic] private static double[] _x, _z;

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
            if (_x == null)
            {
                _x = new double[64];
                _z = new double[64];
            }
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double w = p.PlinthW, d = p.PlinthD > 0 ? p.PlinthD : p.PlinthW, a = d / w;
            int levels = Math.Max(0, p.PlinthLevels);
            double plinthTop = levels * p.StepRiseM;
            uint col = p.Colour != 0 ? p.Colour : p.Stone ? SacredPalette.StoneBuff : SacredPalette.Whitewash;
            double cx, cy, cz;
            k.ToWorld(0, 0, 0, out cx, out cy, out cz);

            // Plinth with the stair on the door side.
            double inset = Math.Min(0.06 * w, 0.4);
            for (int i = 0; i < levels; i++)
            {
                double hw = 0.5 * w - i * inset;
                if (i == 0 && p.OutlineX != null && p.OutlineX.Length >= 3 && Polygon.IsConvex(p.OutlineX, p.OutlineZ, p.OutlineX.Length, 0.0))
                {
                    MeshKit.RingWalls(m, p.OutlineX, p.OutlineZ, p.OutlineX.Length, f.GroundY - 0.4, f.GroundY + p.StepRiseM, SacredPalette.StoneGrey);
                    MeshKit.ConvexCap(m, p.OutlineX, p.OutlineZ, p.OutlineX.Length, f.GroundY + p.StepRiseM, true, SacredPalette.StoneGrey);
                }
                else
                {
                    MeshKit.Box(m, k, -hw, hw, i == 0 ? -0.4 : i * p.StepRiseM, (i + 1) * p.StepRiseM, -hw * a, hw * a, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                }
                if (c != null) c.AddBox(cx, cz, f.GroundY + i * p.StepRiseM, f.GroundY + (i + 1) * p.StepRiseM, hw, hw * a, k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            }
            int steps = 0;
            if (levels > 0)
            {
                double stairW = Math.Max(1.2, 0.25 * w);
                steps = SacredKit.Stair(m, c, k, -0.5 * stairW, 0.5 * stairW, 0.5 * d, plinthTop / Math.Tan(38 * Math.PI / 180), 0, plinthTop, lod,
                                        SacredPalette.StoneGrey, GenColliders.Stone);
            }
            double bodyHW = 0.5 * w - levels * inset;
            double total = p.TotalHeightM > 0 ? p.TotalHeightM : plinthTop + 4 + p.TowerHeightFrac * p.TowerBaseFrac * w;
            double gH = p.GroundStoreyM > 0 ? p.GroundStoreyM : p.Stone ? Math.Min(4.0, 0.22 * total) : Math.Min(0.3 * total, 0.6 * w);
            double gTop = plinthTop + gH;

            // Ground storey: an arcade (stone) or the sanctum body (plaster), with the closed sanctum door.
            MeshKit.Box(m, k, -bodyHW, bodyHW, plinthTop, gTop, -bodyHW * a, bodyHW * a, col, BoxFaces.All & ~BoxFaces.Bottom);
            double sanctumHalf = p.Stone ? 0.55 * bodyHW : bodyHW;
            if (p.Stone && lod <= 1)
            {
                KitFrame side = k;
                for (int sd = 0; sd < 4; sd++)
                {
                    double half = sd % 2 == 0 ? bodyHW * a : bodyHW, along = sd % 2 == 0 ? bodyHW : bodyHW * a;
                    for (int q = 0; q < 5; q++)
                    {
                        double u0 = -along + 2 * along * (q + 0.15) / 5, u1 = -along + 2 * along * (q + 0.85) / 5;
                        if (sd == 0 && q == 2) continue; // the sanctum door bay
                        MeshKit.Panel(m, side, u0, plinthTop + 0.1, u1, gTop - 0.6, half + 0.01, MeshColor.Scale(col, 0.35f));
                    }
                    side = side.TurnedRight();
                }
            }
            SacredKit.SanctumDoor(m, k, 0, plinthTop, bodyHW * a, Math.Min(1.1, 0.3 * bodyHW), Math.Min(2.0, gH - 0.4), SacredPalette.WoodCarved, lod);
            if (c != null) c.AddBox(cx, cz, f.GroundY + plinthTop, f.GroundY + gTop, bodyHW, bodyHW * a, k.UX, k.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);

            // Central curvilinear tower with ratha offsets.
            double finialH = 0.06 * total;
            double towerBase = p.TowerBaseFrac * bodyHW * 2, towerTop = total - finialH;
            double towerH = towerTop - gTop;
            int rathas = Math.Max(8, p.Rathas - p.Rathas % 4);
            int n = lod >= 2 ? 8 : rathas;
            int segs = lod == 0 ? 6 : lod == 1 ? 4 : 2;
            RathaRing(k, 0.5 * towerBase, n, _x, _z);
            double prevS = 1.0, prevY = f.GroundY + gTop;
            for (int sgi = 1; sgi <= segs; sgi++)
            {
                double t = (double)sgi / segs;
                double s = 1.0 - 0.78 * Math.Pow(t, 1.5);
                double y = f.GroundY + gTop + towerH * (1 - Math.Pow(1 - t, 1.15)) * 0.92;
                MeshKit.Loft(m, _x, _z, n, cx, cz, prevY, prevS, y, s, col);
                prevS = s;
                prevY = y;
            }
            // Amalaka (ribbed disc) and kalasha.
            double rTop = prevS * 0.5 * towerBase;
            MeshKit.Frustum(m, cx, cz, rTop * 1.35, prevY, rTop * 1.35, prevY + 0.25 * rTop + 0.15, lod <= 1 ? 12 : 6, true, MeshColor.Scale(col, 0.9f));
            SacredKit.Gajur(m, cx, cz, prevY + 0.25 * rTop + 0.15, f.GroundY + total - (prevY + 0.25 * rTop + 0.15), Math.Max(0.2, 0.6 * rTop), lod, SacredPalette.Gilt);

            // Pavilions (stone): storey 1 around the tower foot on the arcade roof, storey 2 higher up.
            int pinnacles = 1;
            if (p.Stone)
            {
                double pav = Math.Max(1.0, 0.18 * w);
                pinnacles += Pavilions(m, k, p.PavilionsStorey1, bodyHW - 0.6 * pav, gTop, pav, 0.16 * total, col, lod);
                double level2 = gTop + 0.3 * towerH;
                pinnacles += Pavilions(m, k, p.PavilionsStorey2, 0.5 * towerBase * (1.0 - 0.78 * Math.Pow(0.3, 1.5)) + 0.3 * pav, level2, 0.8 * pav, 0.12 * total, col, lod);
            }
            else if (p.Pennant && lod <= 2)
            {
                double x, y, z;
                k.ToWorld(0, 0, 0, out x, out y, out z);
                double py = f.GroundY + total - 0.5 * finialH;
                MeshKit.Tri(m, x, py, z, x, py + 0.9, z, x + 1.4 * k.WX, py + 0.6, z + 1.4 * k.WZ, k.UX, 0, k.UZ, SacredPalette.Sindoor);
                MeshKit.Tri(m, x, py, z, x, py + 0.9, z, x + 1.4 * k.WX, py + 0.6, z + 1.4 * k.WZ, -k.UX, 0, -k.UZ, SacredPalette.Sindoor);
            }
            if (c != null) c.AddBox(cx, cz, f.GroundY + gTop, f.GroundY + total, 0.5 * towerBase, 0.5 * towerBase, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
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
            return m.TriangleCount - t0;
        }

        /// <summary>A star-like ring of n points (n multiple of 4) around the frame origin: alternate radii give the
        /// ratha offsets.</summary>
        private static void RathaRing(in KitFrame f, double r, int n, double[] x, double[] z)
        {
            for (int i = 0; i < n; i++)
            {
                double ang = 2 * Math.PI * i / n + Math.PI / 4;
                double rr = r * (i % 2 == 0 ? 1.0 : 0.86);
                double y;
                f.ToWorld(rr * Math.Cos(ang), 0, rr * Math.Sin(ang), out x[i], out y, out z[i]);
            }
            if (Polygon.SignedArea(x, z, n) < 0)
            {
                Array.Reverse(x, 0, n);
                Array.Reverse(z, 0, n);
            }
        }

        /// <summary>n pavilions on a square ring of half size <paramref name="half"/>: 4 = corners, 8 = corners and
        /// mid-sides, 12 = corners and two per side; each four posts, a cap and a small pointed top.</summary>
        private static int Pavilions(MeshData m, in KitFrame f, int n, double half, double v, double size, double height, uint col, int lod)
        {
            if (n <= 0) return 0;
            var us = new double[n];
            var ws = new double[n];
            int c = 0;
            double[] cu = { 1, -1, -1, 1 }, cw = { 1, 1, -1, -1 };
            for (int i = 0; i < 4 && c < n; i++)
            {
                us[c] = cu[i] * half;
                ws[c] = cw[i] * half;
                c++;
            }
            int perSide = (n - 4) / 4;
            for (int sd = 0; sd < 4 && c < n; sd++)
            {
                for (int q = 0; q < perSide && c < n; q++)
                {
                    double t = perSide == 1 ? 0 : -0.5 + (double)q / (perSide - 1);
                    double along = t * half * (perSide == 1 ? 0 : 1.2);
                    switch (sd)
                    {
                        case 0: us[c] = along; ws[c] = half; break;
                        case 1: us[c] = -half; ws[c] = along; break;
                        case 2: us[c] = along; ws[c] = -half; break;
                        default: us[c] = half; ws[c] = along; break;
                    }
                    c++;
                }
            }
            for (int i = 0; i < c; i++)
            {
                double x, y, z;
                f.ToWorld(us[i], v, ws[i], out x, out y, out z);
                double hs = 0.5 * size, ph = 0.55 * height;
                if (lod <= 1)
                {
                    for (int q = 0; q < 4; q++)
                    {
                        double ox = cu[q] * (hs - 0.1), oz = cw[q] * (hs - 0.1);
                        double px, py, pz;
                        f.ToWorld(us[i] + ox, v, ws[i] + oz, out px, out py, out pz);
                        MeshKit.Cylinder(m, px, pz, 0.08, y, y + ph, 4, false, col);
                    }
                }
                else
                {
                    MeshKit.OrientedBox(m, x, z, y, y + ph, hs * 0.8, hs * 0.8, f.UX, f.UZ, col, BoxFaces.All & ~BoxFaces.Bottom);
                }
                MeshKit.OrientedBox(m, x, z, y + ph, y + ph + 0.2, hs, hs, f.UX, f.UZ, col, BoxFaces.All);
                MeshKit.Frustum(m, x, z, hs, y + ph + 0.2, 0.0, y + height, lod <= 1 ? 8 : 4, false, col);
                if (lod <= 1) MeshKit.Frustum(m, x, z, 0.1, y + height - 0.05, 0.0, y + height + 0.35, 4, false, SacredPalette.Gilt);
            }
            return c;
        }
    }
}
