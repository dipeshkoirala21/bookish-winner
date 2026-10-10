using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>The look of a stupa's upper parts (ref_temples 2).</summary>
    public enum StupaStyle : byte
    {
        /// <summary>White dome, gilt harmika with the eyes, a cone of 13 round gilt rings.</summary>
        Generic = 0,

        /// <summary>Boudhanath: saffron lotus arcs on the dome, red lid lines under the eyes, the red drapery valance, a
        /// stepped square gilt spire, the yellow-skirted crown, chortens on the terrace corners, 147 prayer-wheel niches.</summary>
        Boudha = 1,

        /// <summary>Swayambhunath: saffron streaks on the dome, gilt torana panels over the eyes, round ringed spire, five
        /// gilt Buddha shrines at the dome base.</summary>
        Swayambhu = 2,
    }

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

        /// <summary>Gilt Dhyani Buddha shrines set against the dome (Swayambhu: 5).</summary>
        public int BuddhaNiches;

        public float TotalHeightM;

        /// <summary>The look of the upper parts.</summary>
        public StupaStyle Style;

        /// <summary>A gateway on the door side at this distance from the centre (Boudha's south gate; 0 = none).</summary>
        public float GateDistM;

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
    /// The stupa generator (W2_DESIGN 3.2; temples.md 6.3; ref_temples 2): 0, 1 or 3 twenty-cornered whitewashed
    /// terraces with mouldings, parapets and a stair on the door side; the stepped base ring and a smooth whitewashed
    /// dome (rise 0.33-0.42 × Ø); the gilt harmika with the Buddha's eyes on four faces (white almond eyes, blue irises,
    /// black brows, the nose drawn as "१", the urna dot); 13 gilt rings (a stepped square pyramid at Boudha, a ringed
    /// cone elsewhere), the parasol with its yellow cloth skirt and the finial; prayer-flag lines in the fixed colour
    /// order fanning from the crown to the terrace edges; prayer-wheel niches; Boudha's saffron lotus arcs, drapery,
    /// corner chortens and gate; Swayambhu's torana panels, saffron streaks and five gilt Buddha shrines. Heights are
    /// scaled above the dome base to hit a given total exactly.
    /// </summary>
    public static class StupaGenerator
    {
        [ThreadStatic] private static double[] _x, _z, _pu, _pw;
        [ThreadStatic] private static Profile2 _prof;

        private static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(96))).Clear(false); }
        }

        private static void Scratch()
        {
            if (_x != null) return;
            _x = new double[64];
            _z = new double[64];
            _pu = new double[64];
            _pw = new double[64];
        }

        /// <summary>A square of half size a with a stepped projection in the middle of each side: 20 corners,
        /// counter-clockwise in the frame's (u, w) plane, written as world X/Z.</summary>
        public static int TwentyCorner(in KitFrame f, double a, double[] x, double[] z)
        {
            double[] pu = new double[20], pw = new double[20];
            int n = TwentyCornerLocal(a, pu, pw);
            for (int i = 0; i < n; i++)
            {
                double y;
                f.ToWorld(pu[i], 0, pw[i], out x[i], out y, out z[i]);
            }
            double area = Polygon.SignedArea(x, z, n);
            if (area < 0)
            {
                Array.Reverse(x, 0, n);
                Array.Reverse(z, 0, n);
            }
            return n;
        }

        /// <summary>The twenty-cornered mandala plan in local (u, w): a square of half size a whose sides each step out
        /// twice toward the middle (the projections are 0.2 a and 0.1 a wide each side, 0.035 a deep per step).</summary>
        public static int TwentyCornerLocal(double a, double[] pu, double[] pw)
        {
            double p1 = 0.36 * a, p2 = 0.18 * a, d = 0.035 * a;
            double[] su = { a, p1, p1, p2, p2 };
            double[] sw = { a, a, a + d, a + d, a + 2 * d };
            int n = 0;
            for (int side = 0; side < 4; side++)
            {
                // Corner, then the two outward steps from +u toward the middle, then mirror back.
                for (int k = 0; k < 10; k++)
                {
                    double u, w;
                    if (k < 5)
                    {
                        u = su[k];
                        w = sw[k];
                    }
                    else
                    {
                        u = -su[9 - k];
                        w = sw[9 - k];
                    }
                    if (k == 5) continue; // the middle point is shared: skip the duplicate
                    double ru = u, rw = w;
                    for (int r = 0; r < side; r++)
                    {
                        double t = ru;
                        ru = -rw;
                        rw = t;
                    }
                    if (n < pu.Length)
                    {
                        pu[n] = ru;
                        pw[n] = rw;
                    }
                    n++;
                }
            }
            // Drop the repeated corner of each side (k = 9 equals the next side's k = 0 rotated).
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (Math.Abs(pu[i] - pu[j]) < 1e-9 && Math.Abs(pw[i] - pw[j]) < 1e-9) continue;
                pu[m] = pu[i];
                pw[m] = pw[i];
                m++;
            }
            if (SacredDraw.Area(pu, pw, m) < 0)
            {
                Array.Reverse(pu, 0, m);
                Array.Reverse(pw, 0, m);
            }
            return m;
        }

        public static int Build(in StupaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in StupaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            Scratch();
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double dia = Math.Max(0.5, p.DomeDiameterM), r = 0.5 * dia;
            double cx, cy, cz;
            kf.ToWorld(0, 0, 0, out cx, out cy, out cz);
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
            bool boudha = p.Style == StupaStyle.Boudha, sway = p.Style == StupaStyle.Swayambhu;
            ShapeBrush white = Looks.Whitewash, cream = Looks.Of(SacredPalette.Cream, MaterialChannel.Plaster);
            if (lod >= 3)
            {
                // Box terraces, a coarse dome and a gilt cone (≤ 200 triangles).
                for (int i = 0; i < terraces; i++)
                    SacredDraw.Box(m, k, -0.5 * widths[i], 0.5 * widths[i], i == 0 ? -0.3 : tops[i - 1], tops[i], -0.5 * widths[i], 0.5 * widths[i], white, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Dome(m, SacredDraw.At(k, 0, yDome, 0), white, r, domeRise, 8);
                Shapes.Cone(m, SacredDraw.At(k, 0, yHarm, 0), Looks.Gilt, Math.Max(0.3, 0.5 * p.SpireBaseFrac * dia), top - yHarm, 4, 0, 0, false);
                if (stats != null)
                {
                    stats.Terraces = terraces;
                    stats.Rings = Math.Max(1, p.Rings);
                    stats.TopM = (float)top;
                    stats.DoorYawDeg = f.YawDeg;
                    stats.FlagLines = p.FlagLines;
                }
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }

            // 1. Terraces: mandala plan, base course, coping, a low parapet, a stair on the door side.
            int steps = 0;
            for (int i = 0; i < terraces; i++)
            {
                double y0 = i == 0 ? 0 : tops[i - 1], y1 = tops[i];
                int n = TwentyCornerLocal(0.5 * widths[i], _pu, _pw);
                Mould mo = SacredDraw.M;
                double rise = y1 - y0;
                if (lod >= 2) mo.Add(0, y0 - (i == 0 ? 0.5 : 0), white).Add(0, y1);
                else
                    mo.Add(0.06, y0 - (i == 0 ? 0.5 : 0), cream).Add(0.06, y0 + 0.25).Add(0, y0 + 0.25, white).Add(0, y1 - 0.3).Add(0.1, y1 - 0.3, cream)
                      .Add(0.14, y1 - 0.15, false).Add(0.1, y1);
                SacredDraw.Ring(m, k, _pu, _pw, n, mo);
                SacredDraw.Cap(m, k, _pu, _pw, n, 0.08, y1, true, Looks.Of(MeshColor.FromHex(0xE9E2D2), MaterialChannel.Plaster));
                if (lod <= 1 && rise > 1.0)
                {
                    // Parapet just inside the edge.
                    Mould pp = SacredDraw.M;
                    pp.Add(-0.25, y1, white).Add(-0.25, y1 + 0.55).Add(-0.45, y1 + 0.55, cream).Add(-0.65, y1 + 0.55).Add(-0.65, y1, white);
                    SacredDraw.Ring(m, k, _pu, _pw, n, pp);
                }
                if (c != null) c.AddBox(cx, cz, f.GroundY + y0 - (i == 0 ? 0.5 : 0), f.GroundY + y1, 0.5 * widths[i], 0.5 * widths[i], kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
                double run = i == 0 ? rise / Math.Tan(36 * Math.PI / 180) : Math.Max(0.6, 0.5 * (widths[i - 1] - widths[i]) - 0.1);
                double face = 0.5 * widths[i] + 2 * 0.035 * 0.5 * widths[i];
                double stairW = Math.Min(4.0, 0.08 * widths[i] + 1.0);
                steps += SacredParts.Stair(m, c, kf, k, 0.5 * stairW, face, run, y0, y1, lod <= 1 && rise > 1.0 ? 0.45 : 0, 0.5, Looks.StoneLight, white, lod,
                                           GenColliders.Stone);
                if (boudha && lod == 0)
                {
                    // Small chortens on the outer corners of the terrace.
                    double a = 0.5 * widths[i] - 1.2;
                    for (int q = 0; q < 4; q++)
                        Chorten(m, SacredDraw.At(k, (q % 2 == 0 ? 1 : -1) * a, y1, (q < 2 ? 1 : -1) * a), Math.Min(2.6, 0.035 * widths[i] + 0.6), lod);
                }
            }

            // 2. Prayer-wheel niches round the foot (147 at Boudha, groups of four wheels).
            if (p.WheelNiches && lod <= 1)
            {
                int niches = WheelNiches(m, k, terraces > 0 ? 0.5 * widths[0] : 0.5 * p.DrumDiameterM + 0.6, boudha ? 147 : 0, lod);
                if (stats != null) stats.Niches = niches;
            }

            // 3. Base rings and the dome.
            int seg = lod == 0 ? (dia >= 25 ? 48 : dia >= 9 ? 32 : 20) : lod == 1 ? 24 : 12;
            double drumR = 0.5 * (p.DrumDiameterM > 0 ? p.DrumDiameterM : 1.06 * dia);
            Profile2 pr = Prof;
            pr.Add(0, yDrum - 0.05).Add(drumR, yDrum - 0.05, true);
            if (lod <= 1 && drumR > r + 0.3)
            {
                // Stepped base ring up to the dome foot.
                double st = (drumR - r) / 3.0, sh = drumH / 3.0;
                for (int q = 0; q < 3; q++)
                {
                    pr.Add(drumR - q * st, yDrum + (q + 1) * sh, true);
                    pr.Add(drumR - (q + 1) * st, yDrum + (q + 1) * sh, true);
                }
            }
            else
            {
                pr.Add(drumR, yDome, true).Add(r, yDome, true);
            }
            int rows = lod == 0 ? 12 : lod == 1 ? 8 : 4;
            for (int q = 0; q <= rows; q++)
            {
                double a = 0.5 * Math.PI * q / rows;
                pr.Add(r * Math.Cos(a), yDome + domeRise * Math.Sin(a), q == 0);
            }
            int domeV0 = m.VertexCount;
            Shapes.Lathe(m, k, white, pr, seg);
            if (boudha && lod <= 1) SaffronWash(m, kf, domeV0, f.GroundY + yDome, domeRise, 16);
            if (boudha && lod == 0) LotusArcs(m, k, r, yDome, domeRise, 16, Math.Max(0.25, 0.022 * dia));
            if (sway && lod == 0) Streaks(m, k, r, yDome, domeRise, 28);
            if (boudha && lod <= 1) NicheBand(m, k, r, yDome, domeRise, lod == 0 ? 108 : 36);
            if (p.BuddhaNiches > 0 && lod <= 2)
            {
                for (int q = 0; q < p.BuddhaNiches; q++)
                {
                    // Cardinal shrines first, then the south-east one (Swayambhu's fifth).
                    double ang = q < 4 ? q * 0.5 * Math.PI : 1.25 * Math.PI;
                    Affine3 nf = SacredDraw.Turn(k, 0, yDome, 0, ang);
                    BuddhaShrine(m, nf, r, domeRise, lod);
                }
                if (stats != null) stats.Niches += p.BuddhaNiches;
            }

            // 4. Harmika with the eyes on four faces.
            double hw = 0.5 * Math.Max(0.3, p.HarmikaWFrac * dia);
            ShapeBrush harmB = Looks.GiltAged;
            if (lod >= 2)
            {
                SacredDraw.Box(m, k, -hw, hw, yHarm - 0.3, ySpire, -hw, hw, harmB, BoxFaces.All & ~BoxFaces.Bottom);
            }
            else
            {
                double[] hu2 = ShapeScratch.U2, hw2 = ShapeScratch.W2;
                int hn = SacredDraw.Rect(0, 0, hw, hw, hu2, hw2);
                Mould hm = SacredDraw.M;
                double hh = ySpire - yHarm;
                hm.Add(0.08, yHarm - 0.6, white).Add(0.08, yHarm).Add(0, yHarm, harmB).Add(0, ySpire - 0.12 * hh).Add(0.06 * hh, ySpire - 0.12 * hh, Looks.Gilt)
                  .Add(0.06 * hh, ySpire);
                SacredDraw.Ring(m, k, hu2, hw2, hn, hm);
                SacredDraw.CapRect(m, k, 0, 0, hw + 0.06 * hh, hw + 0.06 * hh, ySpire, true, Looks.Gilt);
                for (int sd = 0; sd < 4; sd++)
                {
                    Affine3 face = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                    Eyes(m, face, hw, yHarm, 0.88 * hh, boudha, lod);
                    if (sway) ToranaPanel(m, face, hw, ySpire, hh, lod);
                }
                if (boudha) Drapery(m, k, hw + 0.06 * hh, ySpire - 0.12 * hh, 0.27 * hh, lod);
                else if (sway) Drapery(m, k, hw + 0.04, yHarm + 0.95 * hh, 0.22 * hh, lod);
            }

            // 5. Thirteen gilt rings, parasol, finial.
            double rb = 0.5 * p.SpireBaseFrac * dia, rt = 0.5 * p.SpireTopFrac * dia;
            int rings = Math.Max(1, p.Rings);
            if (boudha && lod <= 1) SquareSpire(m, k, rb, rt, ySpire, spireH, rings, lod);
            else if (lod <= 1) RingSpire(m, k, rb, rt, ySpire, spireH, rings, lod);
            else Shapes.Frustum(m, SacredDraw.At(k, 0, ySpire, 0), Looks.Gilt, rb, rt, spireH, 6, 0, 0, false, false);
            double rp = 0.5 * p.ParasolFrac * dia;
            Parasol(m, k, rp, yParasol, parasolH, lod);
            SacredParts.Gajur(m, k, yFinial - 0.02, finialH + 0.02, Math.Max(0.15, 0.55 * rt), Looks.Gilt, lod);

            // 6. Prayer-flag lines from below the parasol to the edge of the lowest terrace (or the dome base).
            int lines = lod == 0 ? p.FlagLines : lod == 1 ? p.FlagLines / 3 : 0;
            if (lines > 0)
            {
                // Lines run in bundles of up to four (as at Boudha), fanning a little at their feet.
                int bundle = lines >= 32 ? 4 : lines >= 12 ? 2 : 1, dirs = Math.Max(1, lines / bundle);
                double reach = terraces > 0 ? 0.5 * widths[0] - 0.4 : r * 1.6, endY = terraces > 0 ? tops[0] + 1.6 : 1.2;
                double flag = Math.Max(0.22, Math.Min(0.3, 0.004 * dia + 0.16));
                for (int q = 0; q < lines; q++)
                {
                    int d = q / bundle, j = q % bundle;
                    double ang = 2 * Math.PI * (d + 0.5) / dirs + (j - 0.5 * (bundle - 1)) * 0.035;
                    double rr = reach * (1 - 0.06 * j);
                    double eu = rr * Math.Cos(ang), ew = rr * Math.Sin(ang), su = 0.6 * rp * Math.Cos(ang), sw = 0.6 * rp * Math.Sin(ang);
                    double span = Math.Sqrt((eu - su) * (eu - su) + (ew - sw) * (ew - sw));
                    FlagLine(m, k, su, yParasol - 0.2, sw, eu, endY + 0.3 * j, ew, (0.07 + 0.015 * j) * span, (int)Math.Max(5, Math.Min(16, span / (3.4 * flag))), flag,
                             lod == 0, 0.25);
                }
            }

            // 7. Boudha's south gate.
            if (p.GateDistM > 0 && lod <= 1) Gate(m, k, p.GateDistM, lod);

            if (c != null) c.AddBox(cx, cz, f.GroundY + yDrum, f.GroundY + top, drumR, drumR, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
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
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A point on the dome at compass angle a (radians from +w toward +u) and elevation fraction e (0 foot,
        /// 1 crown), lifted off the surface by <paramref name="lift"/>, with its normal.</summary>
        private static void DomePoint(double r, double y0, double rise, double a, double e, double lift, out double u, out double v, out double w,
                                      out double nu, out double nv, out double nw)
        {
            double phi = 0.5 * Math.PI * e, cr = Math.Cos(phi), sr = Math.Sin(phi);
            double su = Math.Sin(a), sw = Math.Cos(a);
            // Ellipse normal.
            double gr = cr / r, gv = sr / rise, gl = Math.Sqrt(gr * gr + gv * gv);
            nu = gr / gl * su;
            nw = gr / gl * sw;
            nv = gv / gl;
            u = r * cr * su + nu * lift;
            w = r * cr * sw + nw * lift;
            v = y0 + rise * sr + nv * lift;
        }

        /// <summary>Saffron lotus-petal arcs hanging round the dome under the harmika (Boudha): each petal a ribbon of
        /// constant width scalloping down from under the harmika and back up.</summary>
        private static void LotusArcs(MeshData m, in Affine3 k, double r, double y0, double rise, int petals, double width)
        {
            ShapeBrush b = Looks.Of(SacredPalette.Saffron, MaterialChannel.Paint);
            int segs = 14;
            for (int j = 0; j < petals; j++)
            {
                double a0 = 2 * Math.PI * j / petals, a1 = 2 * Math.PI * (j + 1) / petals;
                int first = m.VertexCount;
                for (int q = 0; q <= segs; q++)
                {
                    double t = (double)q / segs, a = a0 + (a1 - a0) * t;
                    double e = Petal(t);
                    double u, v, w, nu, nv, nw, u2, v2, w2, n2u, n2v, n2w;
                    DomePoint(r, y0, rise, a, e, 0.03, out u, out v, out w, out nu, out nv, out nw);
                    double tn = Math.Min(1, t + 0.01), tp = Math.Max(0, t - 0.01);
                    DomePoint(r, y0, rise, a0 + (a1 - a0) * tn, Petal(tn), 0.03, out u2, out v2, out w2, out n2u, out n2v, out n2w);
                    double u3, v3, w3;
                    DomePoint(r, y0, rise, a0 + (a1 - a0) * tp, Petal(tp), 0.03, out u3, out v3, out w3, out n2u, out n2v, out n2w);
                    double tu = u2 - u3, tv = v2 - v3, tw = w2 - w3;
                    // Across the ribbon on the surface: n × t.
                    double cu = nv * tw - nw * tv, cv = nw * tu - nu * tw, cw = nu * tv - nv * tu, cl = Math.Sqrt(cu * cu + cv * cv + cw * cw);
                    if (cl < 1e-9) cl = 1;
                    cu *= 0.5 * width / cl;
                    cv *= 0.5 * width / cl;
                    cw *= 0.5 * width / cl;
                    SacredDraw.V(m, k, u - cu, v - cv, w - cw, nu, nv, nw, b);
                    SacredDraw.V(m, k, u + cu, v + cv, w + cw, nu, nv, nw, b);
                }
                SacredDraw.Grid(m, first, segs + 1, 2, false, null);
            }
        }

        /// <summary>Elevation fraction of the petal outline at fraction t across one petal: U-shaped scallops from just
        /// under the harmika down to the middle of the dome.</summary>
        private static double Petal(double t)
        {
            return 0.97 - 0.55 * Math.Pow(Math.Sin(Math.PI * t), 0.7);
        }

        /// <summary>Tint the dome above the scalloped petal line a pale saffron (Boudha's painted lotus).</summary>
        private static void SaffronWash(MeshData m, in KitFrame kf, int v0, double yDome, double rise, int petals)
        {
            uint wash = MeshColor.FromHex(0xF6D58A);
            for (int v = v0; v < m.VertexCount; v++)
            {
                double y = m.Positions[3 * v + 1] - yDome;
                if (y <= 0) continue;
                double e = Math.Asin(Math.Min(1, y / rise)) / (0.5 * Math.PI);
                double dx = m.Positions[3 * v] - kf.OX, dz = m.Positions[3 * v + 2] - kf.OZ;
                double lu = dx * kf.UX + dz * kf.UZ, lw = dx * kf.UZ - dz * kf.UX;
                double a = Math.Atan2(lu, lw);
                if (a < 0) a += 2 * Math.PI;
                double t = a * petals / (2 * Math.PI);
                t -= Math.Floor(t);
                if (e > Petal(t) + 0.02)
                {
                    m.Colors[4 * v] = (byte)(wash >> 24);
                    m.Colors[4 * v + 1] = (byte)(wash >> 16);
                    m.Colors[4 * v + 2] = (byte)(wash >> 8);
                }
            }
        }

        /// <summary>Saffron splash streaks running down the dome from the harmika (Swayambhu).</summary>
        private static void Streaks(MeshData m, in Affine3 k, double r, double y0, double rise, int count)
        {
            ShapeBrush b = Looks.Of(MeshColor.FromHex(0xF0C040), MaterialChannel.Paint);
            for (int j = 0; j < count; j++)
            {
                uint h = ShapeNoise.Hash(j, 3, 11, 77u);
                double a = 2 * Math.PI * (j + 0.3 * ((h & 255) / 255.0)) / count, len = 0.25 + 0.45 * (((h >> 8) & 255) / 255.0);
                double wa = (0.006 + 0.01 * (((h >> 16) & 255) / 255.0)) * 2 * Math.PI;
                int first = m.VertexCount;
                for (int q = 0; q <= 4; q++)
                {
                    double e = 0.97 - len * q / 4.0, taper = 1 - 0.7 * q / 4.0;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double u, v, w, nu, nv, nw;
                        DomePoint(r, y0, rise, a + side * 0.5 * wa * taper, e, 0.025, out u, out v, out w, out nu, out nv, out nw);
                        SacredDraw.V(m, k, u, v, w, nu, nv, nw, b);
                    }
                }
                SacredDraw.Grid(m, first, 5, 2, false, null);
            }
        }

        /// <summary>The band of small Amitabha niches round the foot of the dome (108 at Boudha).</summary>
        private static void NicheBand(MeshData m, in Affine3 k, double r, double y0, double rise, int n)
        {
            ShapeBrush frame = Looks.GiltAged, dark = Looks.Of(MeshColor.FromHex(0x3A2A1E), MaterialChannel.Plain, 0.7f);
            double nh = Math.Min(0.9, 0.06 * rise + 0.3), nwid = Math.Min(0.6, 2 * Math.PI * r / n * 0.45);
            for (int j = 0; j < n; j++)
            {
                double a = 2 * Math.PI * (j + 0.5) / n;
                double u, v, w, nu, nv, nw;
                DomePoint(r, y0, rise, a, 0.05, 0.02, out u, out v, out w, out nu, out nv, out nw);
                Affine3 f = SacredDraw.Turn(k, u, v, w, a);
                SacredDraw.Box(m, f, -0.5 * nwid - 0.06, 0.5 * nwid + 0.06, 0, nh + 0.08, -0.05, 0.06, frame, BoxFaces.Front | BoxFaces.Top);
                SacredDraw.Panel(m, f, -0.5 * nwid, 0.04, 0.5 * nwid, nh, 0.065, dark);
            }
        }

        /// <summary>A gilt Buddha shrine against the dome base, facing out: a gilt box with an arched dark niche, a seated
        /// gilt figure, a torana and three small pinnacles (Swayambhu).</summary>
        private static void BuddhaShrine(MeshData m, in Affine3 f, double r, double rise, int lod)
        {
            double sw = Math.Max(1.2, 0.13 * r * 2), sh = 1.5 * sw, sd = 0.7 * sw;
            double w0 = r - 0.25 * sd;
            Affine3 b = SacredDraw.At(f, 0, 0, w0);
            ShapeBrush g = Looks.Gilt;
            SacredParts.Pedestal(m, b, 0, 0, 0, 0.5 * sw + 0.1, 0.5 * sd + 0.1, 0.35 * sh, g, lod);
            SacredDraw.Box(m, b, -0.5 * sw, 0.5 * sw, 0.35 * sh, sh, -0.5 * sd, 0.5 * sd, g, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Panel(m, b, -0.3 * sw, 0.42 * sh, 0.3 * sw, 0.85 * sh, 0.5 * sd + 0.01, Looks.Of(MeshColor.FromHex(0x2A1E14), MaterialChannel.Plain, 0.7f));
            if (lod <= 1)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.55 * sh, 0.5 * sd - 0.05), Looks.GiltHi, 0.16 * sw, 0.12 * sh, 0.08 * sw, 6, false);
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.72 * sh, 0.5 * sd - 0.05), Looks.GiltHi, 0.07 * sw, 0.07 * sw, 0.06 * sw, 6, false);
                SacredParts.Torana(m, b, 0, 0.86 * sh, 0.5 * sd, 0.75 * sw, Looks.GiltHi, lod + 1);
            }
            for (int q = -1; q <= 1; q++)
            {
                double ph = q == 0 ? 0.55 * sh : 0.38 * sh, pr = q == 0 ? 0.16 * sw : 0.11 * sw;
                Profile2 p = Prof;
                p.Add(0, 0).Add(pr, 0, true).Add(pr, 0.25 * ph).Add(0.7 * pr, 0.55 * ph).Add(0.3 * pr, 0.8 * ph).Add(0, ph);
                Shapes.Lathe(m, SacredDraw.At(b, q * 0.32 * sw, sh, 0), g, p, lod == 0 ? 8 : 5);
            }
        }

        /// <summary>The Buddha's eyes on one harmika face (the frame's +w face at w = hw): white almond eyes, blue irises,
        /// black pupils, black arched brows, Boudha's red lid lines, the nose drawn as the numeral "१" and the urna dot.</summary>
        private static void Eyes(MeshData m, in Affine3 f, double hw, double v0, double h, bool redLids, int lod)
        {
            double w = hw + 0.012, ew = 0.62 * hw, eh = 0.19 * ew;
            double ve = v0 + 0.4 * h;
            ShapeBrush whiteB = Looks.Of(0xFFFFFFFFu, MaterialChannel.Paint), blue = Looks.PaintBlue, ink = Looks.Ink, red = Looks.PaintRed;
            int seg = lod == 0 ? 8 : 5;
            for (int s = -1; s <= 1; s += 2)
            {
                double uc = s * 0.44 * hw;
                // Almond: the upper lid arcs more than the lower.
                Almond(m, f, uc, ve, w, 0.5 * ew, 1.25 * eh, 0.65 * eh, seg, whiteB);
                Disc(m, f, uc + s * 0.06 * ew, ve - 0.1 * eh, w + 0.006, 0.6 * eh, seg, blue);
                Disc(m, f, uc + s * 0.06 * ew, ve - 0.1 * eh, w + 0.011, 0.3 * eh, Math.Max(5, seg - 2), ink);
                // Brow: a thick black arch.
                Arch(m, f, uc, ve + 1.0 * eh, w + 0.004, 0.62 * ew, 0.85 * eh, 0.32 * eh, seg, ink);
                if (redLids)
                {
                    Arch(m, f, uc, ve - 0.8 * eh, w + 0.004, 0.52 * ew, -0.55 * eh, 0.22 * eh, seg, red);
                    Arch(m, f, uc, ve + 0.62 * eh, w + 0.008, 0.5 * ew, 0.6 * eh, 0.14 * eh, seg, ink);
                }
            }
            // The nose "१": a hooked stroke between and below the eyes, and the urna dot above.
            ShapeBrush noseB = redLids ? red : ink;
            double nu = 0, nv = ve - 0.6 * eh, sz = 0.6 * eh;
            double[] pu = { nu - 0.6 * sz, nu - 0.1 * sz, nu + 0.45 * sz, nu + 0.3 * sz, nu - 0.25 * sz, nu + 0.05 * sz, nu + 0.15 * sz, nu - 0.05 * sz };
            double[] pv = { nv + 0.3 * sz, nv + 0.65 * sz, nv + 0.35 * sz, nv - 0.15 * sz, nv - 0.45 * sz, nv - 0.95 * sz, nv - 1.6 * sz, nv - 2.3 * sz };
            Stroke(m, f, pu, pv, 8, w + 0.006, 0.2 * sz, noseB);
            if (lod <= 1) Disc(m, f, 0, ve + 1.55 * eh, w + 0.006, 0.28 * eh, seg, redLids ? blue : red);
        }

        private static void Almond(MeshData m, in Affine3 f, double uc, double vc, double w, double hu, double up, double down, int seg, in ShapeBrush b)
        {
            int c = SacredDraw.V(m, f, uc, vc, w, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i < 2 * seg; i++)
            {
                double t = (double)i / seg; // 0..2
                double x, y;
                if (t <= 1)
                {
                    x = -hu + 2 * hu * t;
                    y = up * Math.Sin(Math.PI * t);
                }
                else
                {
                    x = hu - 2 * hu * (t - 1);
                    y = -down * Math.Sin(Math.PI * (t - 1));
                }
                SacredDraw.V(m, f, uc + x, vc + y, w, 0, 0, 1, b);
            }
            for (int i = 0; i < 2 * seg; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % (2 * seg));
        }

        private static void Disc(MeshData m, in Affine3 f, double uc, double vc, double w, double rad, int seg, in ShapeBrush b)
        {
            int c = SacredDraw.V(m, f, uc, vc, w, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i < seg; i++)
            {
                double a = 2 * Math.PI * i / seg;
                SacredDraw.V(m, f, uc + rad * Math.Cos(a), vc + rad * Math.Sin(a), w, 0, 0, 1, b);
            }
            for (int i = 0; i < seg; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % seg);
        }

        /// <summary>A flat crescent strip on the plane w: an arch of half width hu rising <paramref name="rise"/> (negative:
        /// hanging) with thickness th, thinning at its tips.</summary>
        private static void Arch(MeshData m, in Affine3 f, double uc, double vc, double w, double hu, double rise, double th, int seg, in ShapeBrush b)
        {
            int first = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double t = (double)i / seg, x = -hu + 2 * hu * t, y = rise * Math.Sin(Math.PI * t), tt = th * (0.35 + 0.65 * Math.Sin(Math.PI * t));
                SacredDraw.V(m, f, uc + x, vc + y, w, 0, 0, 1, b);
                SacredDraw.V(m, f, uc + x, vc + y + tt, w, 0, 0, 1, b);
            }
            SacredDraw.Grid(m, first, seg + 1, 2, false, null);
        }

        /// <summary>A flat stroke of width wd along a polyline in the plane w.</summary>
        private static void Stroke(MeshData m, in Affine3 f, double[] pu, double[] pv, int n, double w, double wd, in ShapeBrush b)
        {
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), c = Math.Min(n - 1, i + 1);
                double tu = pu[c] - pu[a], tv = pv[c] - pv[a], l = Math.Sqrt(tu * tu + tv * tv);
                if (l < 1e-9) l = 1;
                double ou = -tv / l * 0.5 * wd, ov = tu / l * 0.5 * wd;
                SacredDraw.V(m, f, pu[i] - ou, pv[i] - ov, w, 0, 0, 1, b);
                SacredDraw.V(m, f, pu[i] + ou, pv[i] + ov, w, 0, 0, 1, b);
            }
            SacredDraw.Grid(m, first, n, 2, false, null);
        }

        /// <summary>Swayambhu's gilt pentagonal torana panel rising over a harmika face.</summary>
        private static void ToranaPanel(MeshData m, in Affine3 f, double hw, double v, double hh, int lod)
        {
            double pw = 0.95 * hw, ph = 0.9 * hh;
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            x[0] = -pw;
            z[0] = 0;
            x[1] = pw;
            z[1] = 0;
            x[2] = pw;
            z[2] = -0.45 * ph;
            x[3] = 0;
            z[3] = -ph;
            x[4] = -pw;
            z[4] = -0.45 * ph;
            Affine3 b = SacredDraw.Basis(f, 0, v - 0.1, hw + 0.02, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, b, Looks.Gilt, x, z, 5, 0.12, 0.03, 1, BevelStyle.Chamfer, true, false);
            if (lod == 0)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(f, 0, v + 0.32 * ph, hw + 0.15), Looks.GiltHi, 0.22 * pw, 0.22 * ph, 0.06, 6, false);
                Shapes.Ellipsoid(m, SacredDraw.At(f, 0, v + 0.58 * ph, hw + 0.15), Looks.GiltHi, 0.09 * pw, 0.09 * pw, 0.06, 6, false);
            }
        }

        /// <summary>The cloth drapery hung round the harmika top: thin blue, yellow and green bands over a deep red
        /// ruffle (Boudha), or a red-and-green valance (Swayambhu).</summary>
        private static void Drapery(MeshData m, in Affine3 k, double half, double vTop, double depth, int lod)
        {
            ShapeBrush[] bands = { Looks.Of(MeshColor.FromHex(0x2E5DB8), MaterialChannel.Fabric), Looks.ClothYellow,
                                   Looks.Of(MeshColor.FromHex(0x2E9E4F), MaterialChannel.Fabric), Looks.FringeRed };
            double[] hs = { 0.08, 0.08, 0.08, 0.76 };
            for (int s = 0; s < 4; s++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                int n = lod == 0 ? 14 : 6;
                double v = vTop;
                for (int bnd = 0; bnd < 4; bnd++)
                {
                    double h = hs[bnd] * depth;
                    int first = m.VertexCount;
                    for (int row = 0; row < 3; row++)
                    {
                        for (int q = 0; q <= n; q++)
                        {
                            double a = -1 + 2.0 * q / n, x = a * (half + 0.03 * bnd);
                            double scallop = bnd == 3 && row == 2 ? (q % 2 == 0 ? 0 : 0.18 * h) : 0;
                            double bulge = bnd == 3 && row == 1 ? 0.12 * depth : 0.02 * bnd;
                            double vy = v - h * row / 2.0 + scallop;
                            SacredDraw.V(m, side, x, vy, half + 0.03 * bnd + 0.02 + bulge, 0, -0.15 + 0.3 * (1 - row), 1, bands[bnd]);
                        }
                    }
                    SacredDraw.Grid(m, first, 3, n + 1, false, null);
                    v -= h;
                }
            }
        }

        /// <summary>Boudha's spire: thirteen gilt square steps narrowing from rb to rt.</summary>
        private static void SquareSpire(MeshData m, in Affine3 k, double rb, double rt, double y0, double h, int rings, int lod)
        {
            double step = h / rings;
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            for (int q = 0; q < rings; q++)
            {
                double half = rb + (rt - rb) * q / rings, y = y0 + q * step;
                int n = SacredDraw.Rect(0, 0, half, half, pu, pw);
                Mould mo = SacredDraw.M;
                mo.Add(0, y, Looks.Gilt).Add(0, y + 0.72 * step).Add(-0.18 * step, y + 0.72 * step, Looks.GiltAged).Add(-0.18 * step, y + step, Looks.Gilt);
                SacredDraw.Ring(m, k, pu, pw, n, mo);
                if (q == rings - 1) SacredDraw.CapRect(m, k, 0, 0, half - 0.18 * step, half - 0.18 * step, y + step, true, Looks.Gilt);
            }
        }

        /// <summary>A cone of 13 round gilt rings with dark grooves between them (Swayambhu, generic stupas).</summary>
        private static void RingSpire(MeshData m, in Affine3 k, double rb, double rt, double y0, double h, int rings, int lod)
        {
            int seg = lod == 0 ? 16 : 10;
            double step = h / rings;
            // The dark aged core the rings sit on, showing as deep grooves between them.
            Profile2 c = Prof;
            c.Add(0, y0).Add(rb * 0.88, y0, true).Add(rt * 0.88, y0 + h, true).Add(0, y0 + h);
            Shapes.Lathe(m, k, Looks.Of(MeshColor.FromHex(0x5C4317), MaterialChannel.Gilt, 0.55f), c, seg);
            for (int q = 0; q < rings; q++)
            {
                double ra = rb + (rt - rb) * q / rings, rbb = rb + (rt - rb) * (q + 1) / rings, y = y0 + q * step;
                Profile2 p = Prof;
                p.Add(ra * 0.86, y + 0.04 * step).Add(ra * 1.02, y + 0.16 * step, true).Add(0.5 * (ra + rbb) * 1.05, y + 0.42 * step)
                 .Add(rbb * 1.02, y + 0.68 * step, true).Add(rbb * 0.86, y + 0.8 * step);
                Shapes.Lathe(m, k, Looks.Gilt, p, seg);
            }
        }

        /// <summary>The gilt parasol (chhatra) with its yellow cloth skirt.</summary>
        private static void Parasol(MeshData m, in Affine3 k, double rp, double y0, double h, int lod)
        {
            int seg = lod == 0 ? 24 : lod == 1 ? 14 : 8;
            Profile2 p = Prof;
            p.Add(0, y0).Add(0.55 * rp, y0, true).Add(rp, y0 + 0.25 * h, true).Add(0.98 * rp, y0 + 0.4 * h, true).Add(0.5 * rp, y0 + 0.85 * h)
             .Add(0.3 * rp, y0 + h).Add(0, y0 + h);
            Shapes.Lathe(m, k, Looks.Gilt, p, seg);
            if (lod <= 1)
            {
                Profile2 s = Prof;
                s.Add(1.1 * rp, y0 - 0.85 * h).Add(1.06 * rp, y0 - 0.3 * h).Add(1.0 * rp, y0 + 0.24 * h);
                Shapes.Lathe(m, k, Looks.ClothYellow, s, seg);
            }
        }

        /// <summary>A prayer-flag line in local metres: flags of size <paramref name="flag"/> hanging from a sagging line
        /// in the fixed order blue, white, red, green, yellow, from fraction <paramref name="start"/> of the line on (the
        /// upper part near a crown is bare string); two-sided when <paramref name="twoSided"/>.</summary>
        public static void FlagLine(MeshData m, in Affine3 k, double u0, double v0, double w0, double u1, double v1, double w1, double sag, int flags, double flag,
                                    bool twoSided, double start = 0)
        {
            double du = u1 - u0, dw = w1 - w0, l = Math.Sqrt(du * du + dw * dw);
            if (l < 1e-6) return;
            double nu = -dw / l, nw = du / l;
            for (int q = 0; q < flags; q++)
            {
                double a = start + (1 - start) * (q + 0.15) / flags, b = start + (1 - start) * (q + 0.85) / flags;
                double ya = v0 + (v1 - v0) * a - 4 * sag * a * (1 - a), yb = v0 + (v1 - v0) * b - 4 * sag * b * (1 - b);
                double ua = u0 + du * a, wa = w0 + dw * a, ub = u0 + du * b, wb = w0 + dw * b;
                ShapeBrush c = Looks.Of(SacredPalette.Flags[q % 5], MaterialChannel.Fabric);
                double h = 1.15 * flag;
                for (int side = 0; side < (twoSided ? 2 : 1); side++)
                {
                    double s = side == 0 ? 1 : -1;
                    SacredDraw.Quad(m, k, ua, ya, wa, ub, yb, wb, ub, yb - h, wb, ua, ya - h, wa, s * nu, 0.1, s * nw, c);
                }
            }
        }

        /// <summary>The prayer-wheel niches in the kora wall round the foot: n niches (or a 0.5 m pitch of single wheels)
        /// on a square base of half size a, each a dark recess under a red lintel with four gilt and red wheels. Returns the
        /// niches.</summary>
        private static int WheelNiches(MeshData m, in Affine3 k, double a, int n, int lod)
        {
            double per = 8 * a;
            int count = n > 0 ? n : (int)Math.Floor(per / 2.4);
            if (lod == 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                    SacredDraw.Panel(m, side, -a, 0.5, a, 1.3, a + 0.14, Looks.Of(SacredPalette.GiltLo, MaterialChannel.Gilt));
                }
                return count;
            }
            ShapeBrush red = Looks.PaintRed, dark = Looks.Of(MeshColor.FromHex(0x2A1E14), MaterialChannel.Plain, 0.6f);
            double pitch = per / count, nw = Math.Min(2.2, pitch * 0.9);
            for (int j = 0; j < count; j++)
            {
                double d = (j + 0.5) * pitch, along = d % (2 * a), sideIdx = Math.Floor(d / (2 * a));
                double x = -a + along;
                if (Math.Abs(x) > a - 0.6 * nw) continue;
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sideIdx * 0.5 * Math.PI);
                SacredDraw.Panel(m, side, x - 0.5 * nw, 0.45, x + 0.5 * nw, 1.45, a + 0.06, dark);
                SacredDraw.Box(m, side, x - 0.5 * nw - 0.08, x + 0.5 * nw + 0.08, 1.45, 1.6, a + 0.02, a + 0.24, red, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
                for (int q = 0; q < 4; q++)
                {
                    double wu = x - 0.5 * nw + nw * (q + 0.5) / 4;
                    Shapes.Cylinder(m, SacredDraw.At(side, wu, 0.55, a + 0.14), q % 2 == 0 ? Looks.Gilt : red, Math.Min(0.11, 0.1 * nw), 0.8, 5, 0, 0, false, false);
                }
            }
            return count;
        }

        /// <summary>A small white chorten on a terrace corner: stepped base, dome, gilt spire.</summary>
        private static void Chorten(MeshData m, in Affine3 f, double h, int lod)
        {
            Profile2 p = Prof;
            double r = 0.32 * h;
            p.Add(0, 0).Add(r, 0, true).Add(r, 0.12 * h, true).Add(0.8 * r, 0.12 * h, true).Add(0.8 * r, 0.22 * h, true).Add(0.65 * r, 0.22 * h, true)
             .Add(0.7 * r, 0.35 * h).Add(0.6 * r, 0.5 * h).Add(0.25 * r, 0.6 * h, true);
            Shapes.Lathe(m, f, Looks.Whitewash, p, 10);
            Shapes.Cone(m, SacredDraw.At(f, 0, 0.6 * h, 0), Looks.Gilt, 0.2 * r, 0.4 * h, 6, 0, 0, false);
        }

        /// <summary>Boudha's main gate on the door side: two white pillars, a red lintel with a painted band, a small gilt
        /// roof, and a string of flags.</summary>
        private static void Gate(MeshData m, in Affine3 k, double dist, int lod)
        {
            double gw = 6.0, gh = 6.5, pt = 0.8;
            for (int s = -1; s <= 1; s += 2)
                Shapes.RoundedSlab(m, SacredDraw.At(k, s * 0.5 * (gw + pt), 0, dist), Looks.Whitewash, pt, pt, gh, 0.08, 0.06, 1);
            SacredDraw.Box(m, k, -0.5 * gw - pt, 0.5 * gw + pt, gh, gh + 0.9, dist - 0.55, dist + 0.55, Looks.PaintRed, BoxFaces.All);
            SacredDraw.Box(m, k, -0.5 * gw - pt, 0.5 * gw + pt, gh + 0.3, gh + 0.6, dist + 0.55, dist + 0.58, Looks.PaintYellow, BoxFaces.Front);
            Shapes.Frustum(m, SacredDraw.At(k, 0, gh + 0.9, dist), Looks.Gilt, 0.5 * gw + 1.5, 0.6, 1.4, 4, 0, 0, true, false);
            SacredParts.Gajur(m, k, gh + 2.25, 0.9, 0.25, Looks.Gilt, lod + 1);
            FlagLine(m, k, -0.5 * gw - pt, gh + 0.9, dist, 0.5 * gw + pt, gh + 0.9, dist, 0.4, 12, 0.4, lod == 0);
        }
    }
}
