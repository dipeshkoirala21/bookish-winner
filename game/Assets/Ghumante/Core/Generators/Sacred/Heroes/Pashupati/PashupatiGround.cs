using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// The Pashupatinath ground (pashupati-temple.md 2.1): the terrain model is a surface model that reads the roofs round
    /// the courtyard (it climbs 3-5 m over the west buildings), where the real courtyard is one paved level and the west
    /// forecourt and gate passage stand three steps above it. <see cref="Apply"/> flattens a decoded tile's height grid
    /// to those levels inside their zones (site rectangles), cuts the ground standing above them down to the level in a
    /// band round them (<see cref="CutM"/>, for the streamer's decimated terrain) and clamps the terrain beyond to a cone
    /// of slopes back to the original, so the terrain mesh, the sampler, the ground query and every placer read the same
    /// floor the site is built on. The east edge stops at the riverside blocks (a = +28), whose walls drop to the ghat
    /// walk (the river front package's ground) and is steep there; the west forecourt eases into the Deopatan lanes at
    /// 1:4. Idempotent, any tile level; the streamer calls it on every decoded tile before meshing
    /// (<see cref="HeroBuilder.PrepareGround"/>). Also draws the courtyard and forecourt paving.
    /// </summary>
    public static class PashupatiGround
    {
        /// <summary>One level zone: site rectangle, height above the courtyard, the blend slope (rise over run) of the
        /// terrain round it on each side (−a, +a, −b, +b) and the width of the band on each side where the terrain is cut
        /// down to the level (never filled) before the slope starts.</summary>
        private struct Zone
        {
            public double A0, A1, B0, B1, Rise, SlopeA0, SlopeA1, SlopeB0, SlopeB1, CutA0, CutA1, CutB0, CutB1;

            /// <summary>A cut-only zone (<see cref="Cuts"/>): the ground inside is lowered to the level and never raised,
            /// and beyond the zone it is only clamped from above.</summary>
            public bool CutOnly;

            /// <summary>The level at A1 when it differs from <see cref="Rise"/> (at A0): a ramp along a (the east stair's
            /// canyon); NaN for a flat zone.</summary>
            public double RiseA1;

            public double LevelAt(double a)
            {
                if (double.IsNaN(RiseA1)) return Rise;
                double t = (a - A0) / (A1 - A0);
                return Rise + (RiseA1 - Rise) * (t < 0 ? 0 : t > 1 ? 1 : t);
            }
        }

        /// <summary>The riverside walk below the courtyard's east blocks (pashupati-temple.md 2.1: the east stair climbs 4.5 m
        /// in about 25 risers; c_gje/c_01, c_gje/c_03, ov_bag/ref_01): the courtyard's east edge drops to it at the river
        /// faces of the Mukti Mandap and south-east blocks and the east wing. This package only cuts the ground down to it
        /// (never fills); the ghats and the river below are the river front package's (mon-pashupati-river).</summary>
        public const double WalkM = -4.5;

        /// <summary>
        /// Width of the cut bands (m). The streamer draws a level-10 tile's terrain decimated (every second height
        /// sample on the Mid and High tiers, every fourth on Low: <see cref="Ghumante.Core.Streaming.StreamingConfig.TerrainStep"/>),
        /// so a 16 m terrain cell that reaches over the paving from the rising ground round the courtyard would push
        /// through it. The terrain is cut to the level for one 16 m cell's diagonal round the paved zones (cuts only
        /// lower the ground: east of the courtyard, toward the river, it already falls away).
        /// </summary>
        public const double CutM = 23.0;

        /// <summary>The level zones: the courtyard with the riverside blocks and the south court, and the forecourt.</summary>
        private static readonly Zone[] Zones =
        {
            new Zone
            {
                A0 = -36.7, A1 = 24.2, B0 = -24.0, B1 = 31.0, Rise = 0, RiseA1 = double.NaN, SlopeA0 = 0.5, SlopeA1 = 1.2, SlopeB0 = 0.5, SlopeB1 = 0.6, CutA0 = CutM,
                CutA1 = CutM, CutB0 = CutM, CutB1 = CutM,
            },
            new Zone
            {
                A0 = -30.0, A1 = 22.0, B0 = -36.0, B1 = -24.0, Rise = 0, RiseA1 = double.NaN, SlopeA0 = 0.5, SlopeA1 = 1.2, SlopeB0 = 0.5, SlopeB1 = 0.5, CutA0 = CutM,
                CutA1 = 0, CutB0 = CutM, CutB1 = 0,
            },
            new Zone
            {
                A0 = -78.0, A1 = -36.7, B0 = -20.0, B1 = 22.0, Rise = PashupatiSite.ForecourtRiseM, RiseA1 = double.NaN, SlopeA0 = 0.25, SlopeA1 = 0.5, SlopeB0 = 0.3, SlopeB1 = 0.4,
                CutA0 = CutM, CutA1 = 0, CutB0 = CutM, CutB1 = CutM,
            },
        };

        /// <summary>
        /// The cut-only zones east of the courtyard, applied after the level zones: the riverside terrace in front of the
        /// Mukti Mandap and south-east blocks (from their river faces, a 28.4), the walk in front of the east wing (a 31),
        /// both at <see cref="WalkM"/>, and the east stair's canyon, cut 0.35 m under the stair line from the gate sill
        /// (a 24.2, level 0) to the walk (a 34.8), so neither the terrain nor its coarse drawn cells come through the treads.
        /// Beyond each zone the ground is only clamped from above, steeply (1:8) toward the courtyard, whose east edge is
        /// the blocks' vertical river faces and the stair's walls, and 1:1 elsewhere.
        /// </summary>
        private static readonly Zone[] Cuts =
        {
            new Zone
            {
                A0 = 28.4, A1 = 39.0, B0 = -26.0, B1 = 5.0, Rise = WalkM, RiseA1 = double.NaN, SlopeA0 = 8.0, SlopeA1 = 0.6, SlopeB0 = 1.0, SlopeB1 = 1.0,
                CutOnly = true,
            },
            new Zone
            {
                A0 = 31.0, A1 = 39.0, B0 = 5.0, B1 = 30.0, Rise = WalkM, RiseA1 = double.NaN, SlopeA0 = 8.0, SlopeA1 = 0.6, SlopeB0 = 1.0, SlopeB1 = 1.0,
                CutOnly = true,
            },
            new Zone
            {
                A0 = 24.2, A1 = 34.8, B0 = 4.7, B1 = 11.0, Rise = -0.35, RiseA1 = WalkM - 0.35, SlopeA0 = 8.0, SlopeA1 = 1.0, SlopeB0 = 8.0, SlopeB1 = 8.0,
                CutOnly = true,
            },
        };

        /// <summary>Farthest a zone's blend reaches past its cut band (m).</summary>
        public const double BlendReachM = 24.0;

        /// <summary>The level (metres above the courtyard) a site point is flattened to, NaN outside every zone.</summary>
        public static double LevelAt(double a, double b)
        {
            for (int i = 0; i < Zones.Length; i++)
            {
                Zone z = Zones[i];
                if (a >= z.A0 && a <= z.A1 && b >= z.B0 && b <= z.B1) return z.Rise;
            }
            return double.NaN;
        }

        /// <summary>Flatten the site in a decoded tile's height grid (see the class summary); returns the samples changed.
        /// A tile the site does not reach is left alone.</summary>
        public static int Apply(TileData t)
        {
            return Flatten(t, true);
        }

        /// <summary>
        /// The tile as the streamer prepares it, without touching <paramref name="t"/>: the tile itself when it is
        /// already prepared (or the site does not reach it, or it has no heights), else a copy sharing every record but
        /// with its own flattened height grid. The site's models read their ground from this so a build on a raw tile
        /// (tests, tools) sits on the same floor as in the streamed world and never alters a tile someone else holds.
        /// </summary>
        public static TileData Prepared(TileData t)
        {
            if (Flatten(t, false) == 0) return t;
            var copy = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = (ushort[])t.HeightsQ.Clone() };
            Flatten(copy, true);
            return copy;
        }

        /// <summary>The samples <see cref="Apply"/> changes; written only when <paramref name="write"/>.</summary>
        private static int Flatten(TileData t, bool write)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2 || t.HeightsQ.Length < t.HeightsN * t.HeightsN) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            PashupatiFrame f = PashupatiFrame.Plain(t, 0, null);
            // The site's tile-local bounding box with the blend reach.
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < Zones.Length + Cuts.Length; i++)
            {
                Zone zi = i < Zones.Length ? Zones[i] : Cuts[i - Zones.Length];
                for (int q = 0; q < 4; q++)
                {
                    double a = (q & 1) == 0 ? zi.A0 - BlendReachM - CutM : zi.A1 + BlendReachM + CutM;
                    double b = (q & 2) == 0 ? zi.B0 - BlendReachM - CutM : zi.B1 + BlendReachM + CutM;
                    double x, z;
                    f.ToTile(a, b, out x, out z);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }
            }
            if (maxX < 0 || maxZ < 0 || minX > size || minZ > size) return 0;
            int i0 = Math.Max(0, (int)Math.Floor(minX / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling(maxX / cell));
            int j0 = Math.Max(0, (int)Math.Floor(minZ / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling(maxZ / cell));
            // The levelled heights (codes) in a work copy of the bounded window, then the drawn-cell cut over it.
            int w = i1 - i0 + 1, hgt = j1 - j0 + 1;
            var work = new ushort[w * hgt];
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double a, b;
                    f.FromTile(i * cell, j * cell, out a, out b);
                    double h = Ght.Dequantize(t.HeightsQ[j * n + i]);
                    // Inside a zone: its level exactly. Outside: clamp to each zone's cone, the nearest zone last.
                    double inside = LevelAt(a, b);
                    if (!double.IsNaN(inside))
                    {
                        h = f.Level + inside;
                    }
                    else
                    {
                        int nearest = Nearest(a, b);
                        if (nearest >= 0)
                        {
                            for (int z = 0; z < Zones.Length; z++)
                            {
                                if (z == nearest) continue;
                                h = Clamp(Zones[z], f.Level, a, b, h);
                            }
                            h = Clamp(Zones[nearest], f.Level, a, b, h);
                        }
                    }
                    // The cuts east of the courtyard lower the ground (inside to their level, round them along their slope).
                    for (int z = 0; z < Cuts.Length; z++) h = Cut(Cuts[z], f.Level, a, b, h);
                    work[(j - j0) * w + (i - i0)] = Ght.Quantize(h);
                }
            }
            DrawnCut(t, ref f, work, i0, j0, w, hgt);
            int changed = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * n + i;
                    ushort nq = work[(j - j0) * w + (i - i0)];
                    if (nq == t.HeightsQ[k]) continue;
                    if (write) t.HeightsQ[k] = nq;
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>The level zone nearest site (a, b) within the blend reach and cut band, or −1.</summary>
        private static int Nearest(double a, double b)
        {
            int nearest = -1;
            double best = double.MaxValue;
            for (int z = 0; z < Zones.Length; z++)
            {
                double d = Distance(Zones[z], a, b);
                if (d < best)
                {
                    best = d;
                    nearest = z;
                }
            }
            return best <= BlendReachM + CutM ? nearest : -1;
        }

        /// <summary>The lowest height the zones' cones let a sample outside every zone keep (what <see cref="Clamp"/> would
        /// raise it to), or −∞.</summary>
        private static double ConeFloor(double level, double a, double b)
        {
            if (Nearest(a, b) < 0) return double.NegativeInfinity;
            double floor = double.NegativeInfinity;
            for (int z = 0; z < Zones.Length; z++)
            {
                Zone zi = Zones[z];
                double da = a < zi.A0 ? zi.A0 - a : a > zi.A1 ? a - zi.A1 : 0, db = b < zi.B0 ? zi.B0 - b : b > zi.B1 ? b - zi.B1 : 0;
                double ca = a < zi.A0 ? zi.CutA0 : a > zi.A1 ? zi.CutA1 : 0, cb = b < zi.B0 ? zi.CutB0 : b > zi.B1 ? zi.CutB1 : 0;
                if (da > BlendReachM + ca || db > BlendReachM + cb) continue;
                double sa = a < zi.A0 ? zi.SlopeA0 : zi.SlopeA1, sb = b < zi.B0 ? zi.SlopeB0 : zi.SlopeB1;
                floor = Math.Max(floor, level + zi.Rise - Math.Sqrt(Sq(da * sa) + Sq(db * sb)));
            }
            return floor;
        }

        /// <summary>Clearance (m) the drawn terrain keeps under the east stair's line (its treads stand on the line).</summary>
        private const double StairClearM = 0.12;

        /// <summary>
        /// The streamer draws the tile's terrain decimated (<see cref="PashupatiFrame.DrawnSteps"/>: 16 m cells on High and
        /// Mid, 32 m on Low), and a cell spanning the courtyard's edge and the river bank would stand 3-4 m over the east
        /// stair and the riverside terrace below the blocks' river faces, which the full grid's cut alone cannot lower. For
        /// each drawn step, wherever the drawn surface stands above the stair's line (less <see cref="StairClearM"/>) or
        /// above the terrace (<see cref="WalkM"/>), the corners of that triangle are lowered (in whole height codes, most
        /// where the corner weighs most) until it does not. Never a corner inside a level zone (the courtyard and the
        /// forecourt keep their floors on every tier) and never below the zones' cones, so preparing a prepared tile
        /// changes nothing; corners hidden under the riverside blocks or the stair's solid flight may go deep, the others
        /// (the terrace, the walk and the river beyond, the river front package's ground) at most 0.3 m under the walk.
        /// </summary>
        private static void DrawnCut(TileData t, ref PashupatiFrame f, ushort[] work, int i0, int j0, int w, int hgt)
        {
            int[] steps = PashupatiFrame.DrawnSteps(t);
            int n = t.HeightsN;
            double cell = t.Tile.Size / (n - 1);
            // The ceilings: the stair (and its side walls' feet) and the terrace in front of the river faces.
            int nz = (int)Math.Ceiling(PashupatiGates.StairRun / 0.5), nu = 9;
            double hw = PashupatiGates.StairHalfWidth + 0.35, walkA1 = PashupatiGates.WalkEdgeA;
            var pts = new double[3 * ((nz + 1) * nu + 8 * 30 + 6 * 20 + 2 * 8)];
            int np = 0;
            for (int iz = 0; iz <= nz; iz++)
            {
                double z = PashupatiGates.StairRun * iz / nz;
                double cap = PashupatiGates.StairY(z, WalkM) - StairClearM;
                for (int iu = 0; iu < nu; iu++)
                {
                    double a, b;
                    PashupatiGates.StairPoint(z, -hw + 2 * hw * iu / (nu - 1), out a, out b);
                    Add(ref f, pts, ref np, a, b, cap);
                }
            }
            for (int ia = 0; ia < 8; ia++)
            {
                double a = 28.6 + (walkA1 - 28.6) * ia / 7;
                for (int ib = 0; ib < 30; ib++)
                {
                    double b = -24.0 + (4.4 + 24.0) * ib / 29;
                    if (b < -13.0 && a < 32.6) continue; // the south-east block reaches to a 32.6 there
                    Add(ref f, pts, ref np, a, b, WalkM);
                }
            }
            for (int ia = 0; ia < 6; ia++)
            {
                double a = 31.4 + (walkA1 - 31.4) * ia / 5;
                for (int ib = 0; ib < 20; ib++) Add(ref f, pts, ref np, a, 13.6 + (25.0 - 13.6) * ib / 19, WalkM);
            }
            // Fixed corners (inside a level zone) and each corner's floor code.
            var floorQ = new ushort[w * hgt];
            var locked = new bool[w * hgt];
            for (int j = 0; j < hgt; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    double a, b;
                    f.FromTile((i0 + i) * cell, (j0 + j) * cell, out a, out b);
                    int k = j * w + i;
                    locked[k] = !double.IsNaN(LevelAt(a, b));
                    // Floors independent of the heights themselves, so a second pass finds nothing left to lower.
                    double fl = Hidden(a, b) ? f.Level + WalkM - 6.0 : OnWalk(a, b) ? f.Level + WalkM - 0.3 : Ght.Dequantize(work[k]);
                    fl = Math.Max(fl, ConeFloor(f.Level, a, b));
                    floorQ[k] = (ushort)Math.Min(65535, Math.Max(0, Math.Ceiling((fl + 100.0) / 0.15 - 1e-6)));
                }
            }
            // Until nothing moves (every move lowers a corner by at least one code and the floors bound them).
            for (int round = 0; round < 1000; round++)
            {
                bool moved = false;
                for (int si = steps.Length - 1; si >= -1; si--)
                {
                    int st = si < 0 ? 1 : steps[si];
                    for (int p = 0; p < np; p++)
                        moved |= LowerUnder(work, floorQ, locked, i0, j0, w, hgt, n, cell, st, pts[3 * p], pts[3 * p + 1], pts[3 * p + 2]);
                }
                if (!moved) break;
            }
        }

        /// <summary>Whether site (a, b) lies on the riverside terrace or walk below the river faces (cut to <see cref="WalkM"/>).</summary>
        private static bool OnWalk(double a, double b)
        {
            return a > 28.4 && a < 40.0 && b > -30.0 && b < 32.0;
        }

        /// <summary>Whether site (a, b) lies where the drawn terrain is hidden whatever its height: under the riverside
        /// blocks east of the courtyard (the Mukti Mandap's and the south-east block, the east wing) or under the east
        /// stair's solid flight.</summary>
        private static bool Hidden(double a, double b)
        {
            if (a > 24.2 && a < 28.4 && b > -25.5 && b < 4.6) return true;
            if (a > 24.2 && a < 32.2 && b > -25.5 && b < -13.2) return true;
            if (a > 24.2 && a < 31.0 && b > 10.4 && b < 30.0) return true;
            // The stair's footprint in its own frame (z down the stair, u across it).
            double da = Math.Cos(PashupatiGates.StairSkewRad), db = Math.Sin(PashupatiGates.StairSkewRad);
            double ra = a - PashupatiGates.EastFrontA, rb = b - PashupatiGates.EastB;
            double z = ra * da + rb * db, u = ra * db - rb * da;
            return z > 0 && z < PashupatiGates.StairRun && Math.Abs(u) < PashupatiGates.StairHalfWidth + 0.3;
        }

        private static void Add(ref PashupatiFrame f, double[] pts, ref int np, double a, double b, double cap)
        {
            double x, z;
            f.ToTile(a, b, out x, out z);
            pts[3 * np] = x;
            pts[3 * np + 1] = z;
            pts[3 * np + 2] = f.Level + cap;
            np++;
        }

        /// <summary>Lower the free corners of the step-<paramref name="st"/> triangle under tile (x, z) (same diagonal rule
        /// as <see cref="TerrainGrid.Surface"/>) until its surface there is at most <paramref name="cap"/>; true when any
        /// corner moved.</summary>
        private static bool LowerUnder(ushort[] work, ushort[] floorQ, bool[] locked, int i0, int j0, int w, int hgt, int n, double cell, int st, double x,
                                       double z, double cap)
        {
            int q = (n - 1) / st;
            double u = x / (cell * st), v = z / (cell * st);
            if (u < 0 || v < 0 || u > q || v > q) return false;
            int ci = Math.Min(q - 1, (int)Math.Floor(u)), cj = Math.Min(q - 1, (int)Math.Floor(v));
            double fu = u - ci, fv = v - cj;
            // Corners (column, row) in source samples and their barycentric weights.
            int ia = ci * st, ja = cj * st, ib, jb, ic = (ci + 1) * st, jc = (cj + 1) * st;
            double wa, wb, wc;
            if (fu >= fv)
            {
                ib = (ci + 1) * st;
                jb = cj * st;
                wa = 1 - fu;
                wb = fu - fv;
                wc = fv;
            }
            else
            {
                ib = ci * st;
                jb = (cj + 1) * st;
                wa = 1 - fv;
                wb = fv - fu;
                wc = fu;
            }
            int ka = Index(ia, ja, i0, j0, w, hgt), kb = Index(ib, jb, i0, j0, w, hgt), kc = Index(ic, jc, i0, j0, w, hgt);
            if (ka < 0 || kb < 0 || kc < 0) return false;
            double h = wa * Ght.Dequantize(work[ka]) + wb * Ght.Dequantize(work[kb]) + wc * Ght.Dequantize(work[kc]);
            if (h <= cap + 0.005) return false;
            // The least lowering that brings the surface down (each free corner by its weight over the sum of the free
            // weights squared), in whole codes; a corner held at its floor leaves the rest to the next round.
            double sq = (locked[ka] || work[ka] <= floorQ[ka] ? 0 : wa * wa) + (locked[kb] || work[kb] <= floorQ[kb] ? 0 : wb * wb) +
                        (locked[kc] || work[kc] <= floorQ[kc] ? 0 : wc * wc);
            if (sq < 1e-4) return false;
            double d = (h - cap) / sq;
            bool moved = false;
            moved |= Drop(work, floorQ, locked, ka, (int)Math.Ceiling(d * wa / 0.15 - 1e-6));
            moved |= Drop(work, floorQ, locked, kb, (int)Math.Ceiling(d * wb / 0.15 - 1e-6));
            moved |= Drop(work, floorQ, locked, kc, (int)Math.Ceiling(d * wc / 0.15 - 1e-6));
            return moved;
        }

        private static int Index(int i, int j, int i0, int j0, int w, int hgt)
        {
            int li = i - i0, lj = j - j0;
            return li < 0 || lj < 0 || li >= w || lj >= hgt ? -1 : lj * w + li;
        }

        private static bool Drop(ushort[] work, ushort[] floorQ, bool[] locked, int k, int codes)
        {
            if (locked[k] || codes <= 0) return false;
            int to = Math.Max(floorQ[k], work[k] - codes);
            if (to >= work[k]) return false;
            work[k] = (ushort)to;
            return true;
        }

        /// <summary>Lower h to a cut-only zone's level inside it, or to the level plus its slope times the distance outside
        /// it (within <see cref="BlendReachM"/>); never raises it.</summary>
        private static double Cut(in Zone z, double level, double a, double b, double h)
        {
            double da = a < z.A0 ? z.A0 - a : a > z.A1 ? a - z.A1 : 0, db = b < z.B0 ? z.B0 - b : b > z.B1 ? b - z.B1 : 0;
            if (da > BlendReachM || db > BlendReachM) return h;
            double sa = a < z.A0 ? z.SlopeA0 : z.SlopeA1, sb = b < z.B0 ? z.SlopeB0 : z.SlopeB1;
            double above = Math.Sqrt(Sq(da * sa) + Sq(db * sb));
            return Math.Min(h, level + z.LevelAt(a) + above);
        }

        private static double Distance(in Zone z, double a, double b)
        {
            double da = a < z.A0 ? z.A0 - a : a > z.A1 ? a - z.A1 : 0, db = b < z.B0 ? z.B0 - b : b > z.B1 ? b - z.B1 : 0;
            return Math.Sqrt(da * da + db * db);
        }

        /// <summary>Clamp height h to within the zone's cone of slopes: each side's slope times the distance past it
        /// (above the level, past its cut band).</summary>
        private static double Clamp(in Zone z, double level, double a, double b, double h)
        {
            double da = a < z.A0 ? z.A0 - a : a > z.A1 ? a - z.A1 : 0, db = b < z.B0 ? z.B0 - b : b > z.B1 ? b - z.B1 : 0;
            double ca = a < z.A0 ? z.CutA0 : a > z.A1 ? z.CutA1 : 0, cb = b < z.B0 ? z.CutB0 : b > z.B1 ? z.CutB1 : 0;
            if (da > BlendReachM + ca || db > BlendReachM + cb) return h;
            double sa = a < z.A0 ? z.SlopeA0 : z.SlopeA1, sb = b < z.B0 ? z.SlopeB0 : z.SlopeB1;
            double below = Math.Sqrt(Sq(da * sa) + Sq(db * sb)), above = Math.Sqrt(Sq(Math.Max(0, da - ca) * sa) + Sq(Math.Max(0, db - cb) * sb));
            double l = level + z.Rise;
            return Math.Max(l - below, Math.Min(l + above, h));
        }

        private static double Sq(double x)
        {
            return x * x;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Paving
        // ---------------------------------------------------------------------------------------------------------

        private static readonly ShapeBrush Flags = new ShapeBrush(MeshColor.FromHex(0x9F998E), MaterialChannel.Flagstone);
        private static readonly ShapeBrush FlagsLight = new ShapeBrush(MeshColor.FromHex(0xC9C1B2), MaterialChannel.Flagstone);
        private static readonly ShapeBrush Vermilion = new ShapeBrush(MeshColor.FromHex(0xB8432C), MaterialChannel.Dirt);
        private static readonly ShapeBrush Petals = new ShapeBrush(MeshColor.FromHex(0xE89A1C), MaterialChannel.Fabric);
        private static readonly ShapeBrush Runner = new ShapeBrush(MeshColor.FromHex(0xA8402F), MaterialChannel.Fabric);
        private static readonly ShapeBrush RunnerEdge = new ShapeBrush(MeshColor.FromHex(0xE07A2A), MaterialChannel.Fabric);

        /// <summary>The courtyard's grey stone flags (pashupati-temple.md 2.1, PT-17), worn and stained with vermilion and
        /// marigold petals at the doors and round the Nandi; a floor 2 cm over the flattened terrain.</summary>
        internal static void CourtyardFloor(ref PashupatiFrame f, MeshData m)
        {
            if (f.Lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double cellM = f.Lod == 0 ? 6.0 : f.Lod == 1 ? 12.0 : 30.0;
            // Main court, the strip under the riverside blocks south of the east stair and the north-east corner by it.
            Paving(ref f, m, -33.4, 24.2, -24.0, 30.0, 0.02, cellM, Flags, 11u);
            Paving(ref f, m, 24.2, 27.8, -24.0, 4.6, 0.02, cellM, Flags, 12u);
            Paving(ref f, m, 24.2, 27.8, 10.2, 30.0, 0.02, cellM, Flags, 13u);
            Paving(ref f, m, -28.0, 20.0, -34.0, -24.0, 0.02, cellM, Flags, 14u);
            if (f.Lod == 0)
            {
                // Vermilion powder and marigold petals at the four doors and round the Nandi's pedestal.
                for (int s = 0; s < 4; s++)
                {
                    double da = s == 0 ? -1 : s == 2 ? 1 : 0, db = s == 1 ? 1 : s == 3 ? -1 : 0;
                    double ca = da * 11.4, cb = db * 11.6;
                    Stain(ref f, m, ca, cb, 1.6, 0.9, da, db, Vermilion, (uint)s);
                    Stain(ref f, m, ca + 0.4 * db, cb + 0.4 * da, 0.8, 0.5, da, db, Petals, (uint)s + 7);
                }
                Stain(ref f, m, -15.5, 1.9, 1.8, 0.6, 1, 0, Vermilion, 31u);
                Stain(ref f, m, -12.6, -0.6, 0.9, 0.7, 1, 0, Petals, 32u);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>The west forecourt (PT-05): light grey flags three steps above the courtyard and the red runner with its
        /// orange border to the gate.</summary>
        internal static void Forecourt(ref PashupatiFrame f, MeshData m)
        {
            if (f.Lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double y = PashupatiSite.ForecourtRiseM + 0.02, cellM = f.Lod == 0 ? 6.0 : f.Lod == 1 ? 14.0 : 40.0;
            Paving(ref f, m, -76.0, -40.1, -19.0, 21.0, y, cellM, FlagsLight, 21u);
            if (f.Lod <= 1)
            {
                // The runner: 1.4 m of red with an orange edge either side, from the square to the gate.
                const double B = PashupatiGates.WestB;
                Strip(ref f, m, -66.0, -40.1, B - 0.7, B + 0.7, y + 0.012, Runner);
                Strip(ref f, m, -66.0, -40.1, B - 0.82, B - 0.7, y + 0.014, RunnerEdge);
                Strip(ref f, m, -66.0, -40.1, B + 0.7, B + 0.82, y + 0.014, RunnerEdge);
            }
            SacredDraw.Bake(m, v0, i0, f.Level + PashupatiSite.ForecourtRiseM);
        }

        /// <summary>A flat paved rectangle (site a0..a1, b0..b1 at height y) in cells of about <paramref name="cellM"/>,
        /// each cell's tint jittered (worn slabs).</summary>
        internal static void Paving(ref PashupatiFrame f, MeshData m, double a0, double a1, double b0, double b1, double y, double cellM, in ShapeBrush b,
                                    uint seed)
        {
            int na = Math.Max(1, (int)Math.Round((a1 - a0) / cellM)), nb = Math.Max(1, (int)Math.Round((b1 - b0) / cellM));
            // Draped cells, where the terrain's grid rises through the level (round the zone edges, mostly under the
            // buildings, and the strip at the West Gate's courtyard face where the forecourt's step eases down).
            double sub = Math.Max(2.0, 0.25 * cellM);
            for (int i = 0; i < na; i++)
            {
                for (int j = 0; j < nb; j++)
                {
                    double qa0 = a0 + (a1 - a0) * i / na, qa1 = a0 + (a1 - a0) * (i + 1) / na;
                    double qb0 = b0 + (b1 - b0) * j / nb, qb1 = b0 + (b1 - b0) * (j + 1) / nb;
                    uint h = ShapeNoise.Hash(i, j, 3, seed);
                    float k = 0.92f + 0.12f * (h % 1000) / 1000f;
                    ShapeBrush c = b.WithColor(MeshColor.Scale(b.Color, k));
                    int ka = 1, kb = 1;
                    if (Rises(ref f, qa0, qa1, qb0, qb1, y))
                    {
                        ka = Math.Max(1, (int)Math.Ceiling((qa1 - qa0) / sub));
                        kb = Math.Max(1, (int)Math.Ceiling((qb1 - qb0) / sub));
                    }
                    for (int u = 0; u < ka; u++)
                    {
                        for (int w = 0; w < kb; w++)
                        {
                            double sa0 = qa0 + (qa1 - qa0) * u / ka, sa1 = qa0 + (qa1 - qa0) * (u + 1) / ka;
                            double sb0 = qb0 + (qb1 - qb0) * w / kb, sb1 = qb0 + (qb1 - qb0) * (w + 1) / kb;
                            SacredDraw.Quad(m, f.Site, sa0, Drape(ref f, sa0, sb0, y), sb0, sa1, Drape(ref f, sa1, sb0, y), sb0, sa1, Drape(ref f, sa1, sb1, y), sb1,
                                            sa0, Drape(ref f, sa0, sb1, y), sb1, 0, 1, 0, c);
                        }
                    }
                }
            }
        }

        /// <summary>The paving height at site (a, b): <paramref name="y"/>, or 2 cm over the terrain where it rises above
        /// (on any tier's decimation: <see cref="PashupatiFrame.TopGroundAt"/>).</summary>
        private static double Drape(ref PashupatiFrame f, double a, double b, double y)
        {
            return f.Terrain == null ? y : Math.Max(y, f.TopGroundAt(a, b, y) + 0.02);
        }

        /// <summary>Whether the terrain rises through a level cell anywhere (1 m samples).</summary>
        private static bool Rises(ref PashupatiFrame f, double a0, double a1, double b0, double b1, double y)
        {
            if (f.Terrain == null) return false;
            int sa = Math.Max(1, (int)Math.Ceiling(a1 - a0)), sb = Math.Max(1, (int)Math.Ceiling(b1 - b0));
            for (int i = 0; i <= sa; i++)
                for (int j = 0; j <= sb; j++)
                    if (f.TopGroundAt(a0 + (a1 - a0) * i / sa, b0 + (b1 - b0) * j / sb, y) + 0.02 > y + 0.003) return true;
            return false;
        }


        /// <summary>A flat strip on the floor.</summary>
        internal static void Strip(ref PashupatiFrame f, MeshData m, double a0, double a1, double b0, double b1, double y, in ShapeBrush b)
        {
            SacredDraw.Quad(m, f.Site, a0, y, b0, a1, y, b0, a1, y, b1, a0, y, b1, 0, 1, 0, b);
        }

        /// <summary>An irregular flat stain (a jittered octagon) centred on site (a, b), long along (da, db).</summary>
        private static void Stain(ref PashupatiFrame f, MeshData m, double a, double b, double len, double wid, double da, double db, in ShapeBrush br, uint seed)
        {
            const int N = 8;
            int c = SacredDraw.V(m, f.Site, a, 0.035, b, 0, 1, 0, br);
            int first = m.VertexCount;
            for (int i = 0; i < N; i++)
            {
                double t = 2 * Math.PI * i / N, k = 0.75 + 0.5 * (0.5 + 0.5 * ShapeNoise.Unit(i, 1, 5, seed));
                double u = 0.5 * len * Math.Cos(t) * k, w = 0.5 * wid * Math.Sin(t) * k;
                SacredDraw.V(m, f.Site, a + u * da - w * db, 0.03, b + u * db + w * da, 0, 1, 0, br);
            }
            for (int i = 0; i < N; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % N);
        }
    }
}
