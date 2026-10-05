using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// Parameters of a stupa (W2_DESIGN 3.2; temples.md 2.3). Ratios are to the dome diameter; the explicit heights
    /// (metres, 0 = use the ratio) and terrace widths/tops are for heroes (Boudhanath: terraces 81.5 / 62.5 / 50.2 m
    /// with tops at 4 / 8 / 10.5 m, dome to 21 m, harmika to 25 m, rings to 33 m, parasol to 34.5 m, finial to 36 m).
    /// </summary>
    public struct StupaParams
    {
        public float DomeDiameterM;
        public int Terraces;
        public float[] TerraceWidths, TerraceTops;
        public float TerraceRiseFrac;
        public float DrumDiameterM, DrumHeightM;
        public float DomeRiseFrac, HarmikaWFrac, HarmikaHFrac;
        public int Rings;
        public float SpireBaseFrac, SpireTopFrac, SpireHeightFrac, ParasolFrac, FinialFrac;
        public float DomeRiseM, HarmikaHM, SpireHM, ParasolHM, FinialHM;
        public int FlagLines;
        public bool WheelNiches;

        /// <summary>Gilt Dhyani Buddha niche fronts set into the dome (Swayambhu: 5).</summary>
        public int BuddhaNiches;

        public float TotalHeightM;

        public static StupaParams Defaults(float domeDiameter, int terraces)
        {
            return new StupaParams
            {
                DomeDiameterM = domeDiameter, Terraces = terraces, TerraceRiseFrac = 0.11f, DrumDiameterM = 1.06f * domeDiameter,
                DrumHeightM = Math.Max(0.3f, 0.04f * domeDiameter), DomeRiseFrac = 0.37f, HarmikaWFrac = 0.21f, HarmikaHFrac = 0.17f,
                Rings = 13, SpireBaseFrac = 0.24f, SpireTopFrac = 0.12f, SpireHeightFrac = 0.38f, ParasolFrac = 0.15f, FinialFrac = 0.09f,
                FlagLines = domeDiameter >= 8 ? 24 : 0,
            };
        }

        /// <summary>The size class of W2_DESIGN 3.2: S under 8 m, M to 20 m, L above (curated).</summary>
        public static StupaParams ForSize(float footprintM)
        {
            if (footprintM < 8) return Defaults(Math.Max(0.8f, 0.8f * footprintM), 0);
            return Defaults(0.6f * footprintM, 1);
        }
    }

    /// <summary>
    /// The stupa generator (W2_DESIGN 3.2; temples.md 6.3): 0, 1 or 3 twenty-cornered terraces with stairs on the door
    /// side, the drum, a whitewashed dome (rise 0.33-0.42 × Ø), the harmika with the Buddha's eyes on four faces (the
    /// nose drawn as "१", the urna dot) under a gilt torana panel, 13 gilt rings, the parasol and the finial, prayer-flag
    /// lines fanning from the spire, prayer wheels in the base at a 0.5 m pitch and gilt Buddha niches. Heights are
    /// scaled above the dome base to hit a given total exactly.
    /// </summary>
    public static class StupaGenerator
    {
        [ThreadStatic] private static double[] _x, _z;
        [ThreadStatic] private static int[] _tris, _next, _prev;

        /// <summary>A square of half size a with a stepped projection in the middle of each side: 20 corners,
        /// counter-clockwise in the frame's (u, w) plane, written as world X/Z.</summary>
        public static int TwentyCorner(in KitFrame f, double a, double[] x, double[] z)
        {
            double p = 0.2 * a, d = 0.07 * a; // projection half-width and depth
            var us = new double[20];
            var ws = new double[20];
            int n = 0;
            // Side +W (w = a) from +u to -u, then -u side, -W side, +u side; each with a projection.
            for (int side = 0; side < 4; side++)
            {
                // Points along the side from its start corner: corner, inner start, outer start, outer end, inner end.
                double[] su = { a, p, p, -p, -p };
                double[] sw = { a, a, a + d, a + d, a };
                for (int k = 0; k < 5; k++)
                {
                    double u = su[k], w = sw[k];
                    // Rotate (u, w) by side × 90° counter-clockwise in the (u, w) plane.
                    double ru = u, rw = w;
                    for (int r = 0; r < side; r++)
                    {
                        double t = ru;
                        ru = -rw;
                        rw = t;
                    }
                    us[n] = ru;
                    ws[n] = rw;
                    n++;
                }
            }
            for (int i = 0; i < n; i++)
            {
                double y;
                f.ToWorld(us[i], 0, ws[i], out x[i], out y, out z[i]);
            }
            double area = Polygon.SignedArea(x, z, n);
            if (area < 0)
            {
                Array.Reverse(x, 0, n);
                Array.Reverse(z, 0, n);
            }
            return n;
        }

        private static void Scratch()
        {
            if (_x != null) return;
            _x = new double[64];
            _z = new double[64];
            _tris = new int[192];
            _next = new int[64];
            _prev = new int[64];
        }

        public static int Build(in StupaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in StupaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            Scratch();
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double dia = Math.Max(0.5, p.DomeDiameterM), r = 0.5 * dia;
            double cx, cy, cz;
            k.ToWorld(0, 0, 0, out cx, out cy, out cz);
            int terraces = Math.Max(0, p.Terraces);
            double[] widths = new double[terraces], tops = new double[terraces];
            double[] defaultRatio = terraces == 3 ? new[] { 2.4, 1.9, 1.5 } : terraces == 1 ? new[] { 1.5 } : new[] { 2.4, 1.9, 1.5, 1.3 };
            double y = 0;
            for (int i = 0; i < terraces; i++)
            {
                widths[i] = p.TerraceWidths != null && i < p.TerraceWidths.Length ? p.TerraceWidths[i] : defaultRatio[Math.Min(i, defaultRatio.Length - 1)] * dia;
                y = p.TerraceTops != null && i < p.TerraceTops.Length ? p.TerraceTops[i] : y + p.TerraceRiseFrac * dia;
                tops[i] = y;
            }
            double baseTop = terraces > 0 ? tops[terraces - 1] : 0;
            double drumH = p.DrumHeightM, domeRise = p.DomeRiseM > 0 ? p.DomeRiseM : p.DomeRiseFrac * dia;
            double harmH = p.HarmikaHM > 0 ? p.HarmikaHM : p.HarmikaHFrac * dia, spireH = p.SpireHM > 0 ? p.SpireHM : p.SpireHeightFrac * dia;
            double parasolH = p.ParasolHM > 0 ? p.ParasolHM : 0.05 * dia, finialH = p.FinialHM > 0 ? p.FinialHM : p.FinialFrac * dia;
            if (p.TotalHeightM > 0)
            {
                double above = drumH + 0.94 * domeRise + harmH + spireH + parasolH + finialH;
                double s = Math.Max(0.2, (p.TotalHeightM - baseTop) / above);
                drumH *= s;
                domeRise *= s;
                harmH *= s;
                spireH *= s;
                parasolH *= s;
                finialH *= s;
            }
            double yDrum = baseTop, yDome = yDrum + drumH, yHarm = yDome + domeRise * 0.94, ySpire = yHarm + harmH;
            double yParasol = ySpire + spireH, yFinial = yParasol + parasolH, top = yFinial + finialH;
            uint white = SacredPalette.Whitewash;

            // Terraces.
            int steps = 0;
            for (int i = 0; i < terraces; i++)
            {
                double y0 = i == 0 ? -0.5 : tops[i - 1], y1 = tops[i];
                int n = TwentyCorner(k, 0.5 * widths[i], _x, _z);
                MeshKit.RingWalls(m, _x, _z, n, f.GroundY + y0, f.GroundY + y1, white);
                int tri = Polygon.Triangulate(_x, _z, n, _tris, _next, _prev, false);
                for (int q = 0; q < tri; q++)
                {
                    int a = _tris[3 * q], b = _tris[3 * q + 1], cc = _tris[3 * q + 2];
                    MeshKit.Tri(m, _x[a], f.GroundY + y1, _z[a], _x[b], f.GroundY + y1, _z[b], _x[cc], f.GroundY + y1, _z[cc], 0, 1, 0, SacredPalette.Cream);
                }
                if (c != null) c.AddBox(cx, cz, f.GroundY + y0, f.GroundY + y1, 0.5 * widths[i], 0.5 * widths[i], k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
                double run = i == 0 ? (y1 - Math.Max(0, y0)) / Math.Tan(38 * Math.PI / 180) : Math.Max(0.6, 0.5 * (widths[i - 1] - widths[i]));
                double face = 0.5 * widths[i] + 0.07 * 0.5 * widths[i];
                double stairW = Math.Min(4.0, 0.08 * widths[i] + 1.0);
                steps += SacredKit.Stair(m, c, k, -0.5 * stairW, 0.5 * stairW, face, run, Math.Max(0, y0), y1, lod, white, GenColliders.Stone);
            }
            // Prayer wheels in niches along the base of the lowest terrace (or the drum), 0.5 m pitch.
            if (p.WheelNiches && lod <= 1)
            {
                double a = terraces > 0 ? 0.5 * widths[0] : 0.5 * p.DrumDiameterM;
                int perSide = (int)Math.Floor(2 * a / 0.5);
                KitFrame side = k;
                for (int sd = 0; sd < 4; sd++)
                {
                    if (lod == 1)
                    {
                        MeshKit.Panel(m, side, -a, 0.6, a, 1.1, a + 0.13, SacredPalette.GiltLo);
                    }
                    else
                    {
                        for (int q = 0; q < perSide; q++)
                        {
                            double u = -a + 0.25 + q * 0.5;
                            double x, yy, z;
                            side.ToWorld(u, 0, a + 0.15, out x, out yy, out z);
                            MeshKit.Cylinder(m, x, z, 0.11, f.GroundY + 0.6, f.GroundY + 1.0, 4, false, q % 2 == 0 ? SacredPalette.GiltLo : SacredPalette.WoodRed);
                        }
                    }
                    side = side.TurnedRight();
                }
                if (stats != null) stats.Niches = 4 * perSide;
            }

            // Drum and dome.
            int sides = lod == 0 ? 32 : lod == 1 ? 20 : lod == 2 ? 12 : 8;
            int bands = lod == 0 ? 6 : lod == 1 ? 4 : 2;
            double drumR = 0.5 * (p.DrumDiameterM > 0 ? p.DrumDiameterM : 1.06 * dia);
            MeshKit.Cylinder(m, cx, cz, drumR, f.GroundY + yDrum - 0.05, f.GroundY + yDome, sides, true, white);
            MeshKit.Dome(m, cx, cz, r, f.GroundY + yDome, domeRise, sides, bands, white);
            if (p.BuddhaNiches > 0 && lod <= 2)
            {
                for (int q = 0; q < p.BuddhaNiches; q++)
                {
                    // Cardinal niches first, then the south-east one (Swayambhu's fifth).
                    double ang = q < 4 ? q * 0.5 * Math.PI : 1.75 * Math.PI;
                    var nf = new KitFrame(cx, f.GroundY + yDome, cz, -Math.Sin(ang), Math.Cos(ang));
                    double out_ = r * 0.95;
                    Niche(nf, m, out_, r);
                }
                if (stats != null) stats.Niches += p.BuddhaNiches;
            }

            // Harmika with the eyes on four faces.
            double hw = 0.5 * Math.Max(0.3, p.HarmikaWFrac * dia);
            MeshKit.Box(m, k, -hw, hw, yHarm, ySpire, -hw, hw, white, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 2)
            {
                KitFrame face = k;
                for (int sd = 0; sd < 4; sd++)
                {
                    Eyes(m, face, hw, yHarm, ySpire - yHarm, lod);
                    face = face.TurnedRight();
                }
            }

            // Thirteen gilt rings, parasol, finial.
            double rb = 0.5 * p.SpireBaseFrac * dia, rt = 0.5 * p.SpireTopFrac * dia;
            int rings = Math.Max(1, p.Rings);
            if (lod <= 1)
            {
                double ringH = spireH / rings;
                int rs = lod == 0 ? 10 : 6;
                for (int q = 0; q < rings; q++)
                {
                    double ra = rb + (rt - rb) * q / rings, rbb = rb + (rt - rb) * (q + 1) / rings;
                    MeshKit.Frustum(m, cx, cz, ra, f.GroundY + ySpire + q * ringH, rbb, f.GroundY + ySpire + (q + 0.82) * ringH, rs, true,
                                    q % 2 == 0 ? SacredPalette.Gilt : SacredPalette.GiltHi);
                }
                MeshKit.Cylinder(m, cx, cz, 0.7 * rt, f.GroundY + ySpire, f.GroundY + yParasol, rs, false, SacredPalette.GiltLo);
            }
            else
            {
                MeshKit.Frustum(m, cx, cz, rb, f.GroundY + ySpire, rt, f.GroundY + yParasol, 6, true, SacredPalette.Gilt);
            }
            double rp = 0.5 * p.ParasolFrac * dia;
            MeshKit.Frustum(m, cx, cz, rp, f.GroundY + yParasol, 0.6 * rp, f.GroundY + yParasol + parasolH, lod <= 1 ? 12 : 6, true, SacredPalette.Gilt);
            MeshKit.Frustum(m, cx, cz, rp, f.GroundY + yParasol, rp * 0.98, f.GroundY + yParasol - 0.05, lod <= 1 ? 12 : 6, false, SacredPalette.GiltLo);
            SacredKit.Gajur(m, cx, cz, f.GroundY + yFinial, finialH, Math.Max(0.15, 0.5 * rt), lod, SacredPalette.Gilt);

            // Prayer-flag lines from below the parasol to the edge of the lowest terrace (or the dome base).
            int lines = lod == 0 ? p.FlagLines : lod == 1 ? p.FlagLines / 3 : 0;
            if (lines > 0)
            {
                double reach = terraces > 0 ? 0.5 * widths[0] : r * 1.6, endY = terraces > 0 ? tops[0] + 1.5 : yDome + 0.5;
                for (int q = 0; q < lines; q++)
                {
                    double ang = 2 * Math.PI * (q + 0.5) / lines;
                    double ex = cx + reach * Math.Cos(ang), ez = cz + reach * Math.Sin(ang);
                    double sx = cx + 0.4 * rt * Math.Cos(ang), sz = cz + 0.4 * rt * Math.Sin(ang);
                    double span = Math.Sqrt((ex - sx) * (ex - sx) + (ez - sz) * (ez - sz));
                    SacredKit.FlagLine(m, sx, f.GroundY + yParasol - 0.3, sz, ex, f.GroundY + endY, ez, 0.1 * span, 8, 0.3);
                }
            }
            if (c != null) c.AddBox(cx, cz, f.GroundY + yDrum, f.GroundY + top, drumR, drumR, k.UX, k.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
            if (stats != null)
            {
                stats.Terraces = terraces;
                stats.Rings = rings;
                stats.TopM = (float)top;
                stats.PlinthTopM = (float)baseTop;
                stats.DoorYawDeg = f.YawDeg;
                stats.Steps = steps;
                stats.FlagLines = p.FlagLines;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>The Buddha's eyes on one harmika face (the frame's +W face at w = hw): brows, eyes with pupils, the
        /// nose drawn as the numeral "१" (a hooked stroke), the urna dot and the gilt torana above.</summary>
        private static void Eyes(MeshData m, in KitFrame f, double hw, double v0, double h, int lod)
        {
            double w = hw + 0.01, ew = 0.3 * hw, eh = 0.12 * h;
            double ve = v0 + 0.45 * h;
            uint ink = SacredPalette.EyesInk;
            for (int s = -1; s <= 1; s += 2)
            {
                double uc = s * 0.45 * hw;
                MeshKit.Panel(m, f, uc - 0.5 * ew, ve - 0.5 * eh, uc + 0.5 * ew, ve + 0.5 * eh, w, MeshColor.FromHex(0xFFFFFF));
                MeshKit.Panel(m, f, uc - 0.15 * ew, ve - 0.4 * eh, uc + 0.25 * ew, ve + 0.4 * eh, w + 0.005, SacredPalette.EyesBlue);
                if (lod <= 1) MeshKit.Panel(m, f, uc - 0.55 * ew, ve + 0.9 * eh, uc + 0.55 * ew, ve + 1.25 * eh, w, ink);
            }
            // Nose as "१": a vertical stroke with a hook at the top.
            MeshKit.Panel(m, f, -0.04 * hw, ve - 1.8 * eh, 0.04 * hw, ve - 0.2 * eh, w, ink);
            if (lod <= 1)
            {
                MeshKit.Panel(m, f, -0.04 * hw, ve - 0.35 * eh, 0.14 * hw, ve - 0.2 * eh, w, ink);
                MeshKit.Panel(m, f, -0.03 * hw, ve + 1.5 * eh, 0.03 * hw, ve + 1.7 * eh, w, SacredPalette.Sindoor); // urna dot
            }
            SacredKit.Torana(m, f, 0, v0 + 0.78 * h, w, 1.2 * hw, SacredPalette.Gilt, lod);
        }

        /// <summary>A gilt niche front set into the dome: a small framed panel facing the frame's +W.</summary>
        private static void Niche(in KitFrame f, MeshData m, double out_, double r)
        {
            double nw = 0.12 * r, nh = 0.16 * r;
            MeshKit.Box(m, f, -0.5 * nw - 0.1, 0.5 * nw + 0.1, 0.05 * r, 0.05 * r + nh + 0.15, out_ - 0.05, out_ + 0.25, SacredPalette.Gilt, BoxFaces.Wall);
            MeshKit.Panel(m, f, -0.5 * nw, 0.05 * r + 0.05, 0.5 * nw, 0.05 * r + nh, out_ + 0.255, SacredPalette.GiltLo);
        }
    }
}
