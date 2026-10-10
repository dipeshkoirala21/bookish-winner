using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;

namespace Ghumante.Core.Meshing
{
    /// <summary>What a house needs to know about its surroundings: the road corridors (overhang clearance) and the
    /// other footprints of the tile (corners are only rounded where nothing abuts them).</summary>
    internal struct HouseEnv
    {
        public Clearance Clear;
        public BuildingBands.FootprintIndex Neighbours;
        public int Index;
    }

    /// <summary>
    /// The B0 facade and roof grammar for one house (W2_DESIGN 2.3, 2.6; docs/research/w2/ref_buildings.md for what each
    /// place looks like today). The footprint is split into plots along the street front (4-8 m in the split profiles);
    /// every plot is its own house: archetype draw, storey count, palette, eave or parapet height and roof, so a merged
    /// footprint reads as the row of houses it is. Each plot is clipped from the footprint, its exposed convex corners
    /// rounded, its body walls raised (raw brick sides on painted houses, a brick-to-plaster seam on hybrids) on a stone
    /// or concrete plinth, its street facade cut with real openings (recessed windows with frames, lattice, doors,
    /// shop bays) and dressed per archetype (<c>HouseBuilder.Facades.cs</c>), and its roof built
    /// (<c>HouseBuilder.Roofs.cs</c>): a jhingati gable on struts, or a flat terrace with parapet, coping, stair cabin and
    /// the roof props. Nothing below <see cref="Roads.RoadClearance.MinOverheadClearanceM"/> projects into a road
    /// corridor (<see cref="Clearance"/>). Every vertex carries its <see cref="MaterialChannel"/> and baked AO in UV0.
    /// The drop level removes detail in the W2_DESIGN §2.4 order (lattice relief and small props, struts and tile
    /// courses, floor bands and railings, roof props); the sanjhya is never dropped.
    /// </summary>
    internal static partial class HouseBuilder
    {
        public const int MaxDrop = 4;
        private const uint PurposePlots = 0x504C4F54;
        private const uint PurposeFacade = 0x46414344;
        private const uint PurposeProps = 0x50524F50;
        private const uint PurposePlotArch = 0x50415243;
        private const int MaxPlots = 16, MaxFloors = 18;

        private sealed class Scratch
        {
            public double[] X = new double[64], Z = new double[64];
            public double[] PlotU = new double[MaxPlots + 2];
            public double[] PX = new double[128], PZ = new double[128];
            public double[] DX = new double[256], DZ = new double[256];
            public byte[] DK = new byte[256];
            public double[] NX = new double[256], NZ = new double[256];
            public int[] Tris = new int[768], Next = new int[256], Prev = new int[256];
            public double[] Floors = new double[(MaxPlots + 1) * MaxFloors];
            public Plot[] Plots = new Plot[MaxPlots + 1];
            public KitHole[] Holes = new KitHole[48];

            public void Ensure(int n)
            {
                if (X.Length < n)
                {
                    int cap = Math.Max(n, X.Length * 2);
                    X = new double[cap];
                    Z = new double[cap];
                }
                int need = 4 * n + 16;
                if (PX.Length < need)
                {
                    PX = new double[need];
                    PZ = new double[need];
                }
                int dneed = 3 * need;
                if (DX.Length < dneed)
                {
                    DX = new double[dneed];
                    DZ = new double[dneed];
                    DK = new byte[dneed];
                    NX = new double[dneed];
                    NZ = new double[dneed];
                    Tris = new int[3 * dneed];
                    Next = new int[dneed];
                    Prev = new int[dneed];
                }
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        /// <summary>Detail switches for a drop level.</summary>
        private struct Detail
        {
            /// <summary>Lattice screens are never dropped (the identity of a Newar window); they get coarser.</summary>
            public double LatticePitch;

            public bool Small, Struts, Courses, Grilles, Bands, Rails, Props;
            public int Segs;

            public static Detail For(int drop)
            {
                return new Detail
                {
                    LatticePitch = drop < 1 ? 0.16 : drop < 2 ? 0.19 : drop < 4 ? 0.24 : 0.3, Small = drop < 1, Struts = drop < 2, Courses = drop < 2,
                    Grilles = drop < 2, Bands = drop < 3, Rails = drop < 3, Props = drop < 4, Segs = drop < 1 ? 2 : drop < 3 ? 1 : 0,
                };
            }
        }

        private enum Edge : byte
        {
            Body = 0,
            Front = 1,
            PartitionLow = 2,
            PartitionHigh = 3,
            Arc = 4,
        }

        /// <summary>Everything the facade code needs about one building.</summary>
        private struct House
        {
            public HousePlan Plan;
            public StyleParams Style;
            public KitFrame F;
            public double L, D;
            public double Ground, Base;
            public int Drop;
            public Detail Det;
            public GenColliders Colliders;
            public double CX, CZ, AreaM2;
            public Clearance Clear;
            public BuildingGround G;
            public BuildingBands.FootprintIndex Neighbours;
            public int Index;
            public int Plots;
            public bool Corner;
            public double SecondAX, SecondAZ, SecondBX, SecondBZ;
        }

        /// <summary>One house of a (possibly merged) footprint.</summary>
        private struct Plot
        {
            public int Index;
            public double U0, U1, FU0, FU1, Depth;
            public BuildingArchetype Arch;
            public int Storeys;
            public double Plinth, Top, Parapet;
            public bool Flat, Gable, Shop, Rect, TileHood, Glazed, ExposedBrick, PaintedSides;
            public uint Front, Wall, Trim, Wood, Roof, Seed;
            public MaterialChannel FrontCh, WallCh;
            public int PolyStart, PolyCount, DispStart, DispCount;
            public double RidgeV, Cant;
        }

        /// <summary>Build one house at a drop level; returns false when the footprint is degenerate.</summary>
        public static bool Build(BuildingRecord b, in HousePlan plan, ref BuildingGround g, in HouseEnv env, float sinkM, int drop, MeshData m,
                                 GenColliders c)
        {
            Scratch s = _scratch ?? (_scratch = new Scratch());
            int n = LoadRing(b.Rings[0], s);
            if (n < 3) return false;
            double area = Polygon.SignedArea(s.X, s.Z, n);
            if (area < 0)
            {
                Array.Reverse(s.X, 0, n);
                Array.Reverse(s.Z, 0, n);
                area = -area;
            }
            if (area < 1.0) return false;
            s.Ensure(n + 8);
            int vStart = m.VertexCount;
            int fe = FrontIndex(b.Rings[0], plan.FrontEdge, s, n);
            double ax = s.X[fe], az = s.Z[fe];
            int fj = fe + 1 == n ? 0 : fe + 1;
            double bx = s.X[fj], bz = s.Z[fj];
            double cxm, czm;
            Polygon.Centroid(s.X, s.Z, n, out cxm, out czm);
            double ground = double.MaxValue;
            for (int i = 0; i < n; i++) ground = Math.Min(ground, g.Height(s.X[i], s.Z[i]));
            ground = Math.Min(ground, g.Height(cxm, czm));
            var h = new House
            {
                Plan = plan, Style = BuildingGrammar.For(plan.Profile), F = new KitFrame(ax, ground, az, bx - ax, bz - az), Ground = ground, Drop = drop,
                Det = Detail.For(drop), Colliders = c, CX = cxm, CZ = czm, AreaM2 = area, Clear = env.Clear, G = g, Neighbours = env.Neighbours,
                Index = env.Index,
            };
            h.L = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (h.L < 1.0) return false;
            double depth = 0;
            for (int i = 0; i < n; i++)
            {
                double w = (s.X[i] - ax) * h.F.WX + (s.Z[i] - az) * h.F.WZ;
                if (-w > depth) depth = -w;
            }
            h.D = Math.Max(1.0, depth);
            h.Base = plan.MinHeightM > 0 ? ground + plan.MinHeightM : ground - sinkM;
            int se = SecondIndex(b.Rings[0], plan.SecondEdge, s, n, fe);
            if (se >= 0)
            {
                int sj = se + 1 == n ? 0 : se + 1;
                h.Corner = true;
                h.SecondAX = s.X[se];
                h.SecondAZ = s.Z[se];
                h.SecondBX = s.X[sj];
                h.SecondBZ = s.Z[sj];
            }

            int plots = Plots(ref h, s);
            h.Plots = plots;
            int used = 0;
            for (int p = 0; p < plots; p++)
            {
                Plot pl = default(Plot);
                pl.Index = p;
                pl.U0 = s.PlotU[p];
                pl.U1 = s.PlotU[p + 1];
                pl.PolyStart = used;
                pl.PolyCount = ClipPlot(ref h, s, n, p == 0 ? double.NegativeInfinity : pl.U0, p == plots - 1 ? double.PositiveInfinity : pl.U1, used);
                used += pl.PolyCount;
                Measure(ref h, s, ref pl);
                Configure(ref h, s, ref pl);
                s.Plots[p] = pl;
            }
            int disp = 0;
            for (int p = 0; p < plots; p++)
            {
                Plot pl = s.Plots[p];
                if (pl.PolyCount < 3) continue;
                pl.DispStart = disp;
                pl.DispCount = Display(ref h, s, ref pl, env, disp);
                disp += pl.DispCount;
                s.Plots[p] = pl;
            }
            for (int p = 0; p < plots; p++)
            {
                Plot pl = s.Plots[p];
                if (pl.PolyCount < 3 || pl.DispCount < 3) continue;
                var rng = new GrammarRng(pl.Seed, PurposeFacade);
                BodyWalls(ref h, s, ref pl, m);
                Facade(ref h, s, ref pl, ref rng, m);
                Roof(ref h, s, ref pl, ref rng, m);
            }

            // Courtyard walls of holes, facing into the courtyard (no roof over the courtyard).
            for (int r = 1; r < b.Rings.Length; r++)
            {
                int hn = LoadRing(b.Rings[r], s);
                if (hn < 3) continue;
                double ha = Polygon.SignedArea(s.X, s.Z, hn);
                if (Math.Abs(ha) < 0.25) continue;
                if (ha > 0)
                {
                    Array.Reverse(s.X, 0, hn);
                    Array.Reverse(s.Z, 0, hn);
                }
                int v0 = m.VertexCount;
                MeshKit.RingWalls(m, s.X, s.Z, hn, h.Base, ground + s.Plots[0].Top, s.Plots[0].Wall);
                WallPaint(ref h, s.Plots[0].WallCh, 1f).Apply(m, v0);
            }
            h.Clear.Clamp(m, vStart, ref h.G);
            g = h.G;
            return true;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Footprint and plots
        // -------------------------------------------------------------------------------------------------------------

        private static int LoadRing(int[] ring, Scratch s)
        {
            int count = ring.Length / 2;
            s.Ensure(count + 1);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                double x = ring[2 * i] / 100.0, z = ring[2 * i + 1] / 100.0;
                if (n > 0 && s.X[n - 1] == x && s.Z[n - 1] == z) continue;
                s.X[n] = x;
                s.Z[n] = z;
                n++;
            }
            while (n > 1 && s.X[n - 1] == s.X[0] && s.Z[n - 1] == s.Z[0]) n--;
            return n;
        }

        /// <summary>The loaded-ring index of the plan's front edge (by its start point), else the longest edge.</summary>
        private static int FrontIndex(int[] ring, int front, Scratch s, int n)
        {
            if (front >= 0 && front < ring.Length / 2)
            {
                double x = ring[2 * front] / 100.0, z = ring[2 * front + 1] / 100.0;
                for (int i = 0; i < n; i++)
                    if (s.X[i] == x && s.Z[i] == z)
                    {
                        // The ring may have been reversed: the front then starts at the edge's other end.
                        int j = i + 1 == n ? 0 : i + 1, k = i == 0 ? n - 1 : i - 1;
                        int fj = front + 1 == ring.Length / 2 ? 0 : front + 1;
                        double ex = ring[2 * fj] / 100.0, ez = ring[2 * fj + 1] / 100.0;
                        if (s.X[j] == ex && s.Z[j] == ez) return i;
                        if (s.X[k] == ex && s.Z[k] == ez) return k;
                        return i;
                    }
            }
            int best = 0;
            double bl = -1;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double l = (s.X[j] - s.X[i]) * (s.X[j] - s.X[i]) + (s.Z[j] - s.Z[i]) * (s.Z[j] - s.Z[i]);
                if (l > bl)
                {
                    bl = l;
                    best = i;
                }
            }
            return best;
        }

        private static int SecondIndex(int[] ring, int second, Scratch s, int n, int fe)
        {
            if (second < 0 || second >= ring.Length / 2) return -1;
            double x = ring[2 * second] / 100.0, z = ring[2 * second + 1] / 100.0;
            int sj = second + 1 == ring.Length / 2 ? 0 : second + 1;
            double ex = ring[2 * sj] / 100.0, ez = ring[2 * sj + 1] / 100.0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1, k = i == 0 ? n - 1 : i - 1;
                if (s.X[i] == x && s.Z[i] == z && s.X[j] == ex && s.Z[j] == ez) return i == fe ? -1 : i;
                if (s.X[i] == ex && s.Z[i] == ez && s.X[j] == x && s.Z[j] == z) return i == fe ? -1 : i;
                if (s.X[i] == x && s.Z[i] == z && s.X[k] == ex && s.Z[k] == ez) return k == fe ? -1 : k;
            }
            return -1;
        }

        /// <summary>Plot boundaries along the front (W2_DESIGN 2.3 plot split): fronts over 9 m in the split profiles
        /// become 4-8 m plots.</summary>
        private static int Plots(ref House h, Scratch s)
        {
            s.PlotU[0] = 0;
            if (!h.Style.PlotSplit || h.L <= 9.0 || h.Plan.Archetype == BuildingArchetype.RanaPalace || h.Plan.Archetype == BuildingArchetype.Generic && h.Plan.Profile == StyleProfile.None)
            {
                s.PlotU[1] = h.L;
                return 1;
            }
            var rng = new GrammarRng(h.Plan.Seed, PurposePlots);
            int k = 0;
            double u = 0;
            while (h.L - u > 8.0 && k < MaxPlots - 1)
            {
                double w = rng.Range(4f, 8f);
                if (h.L - (u + w) < 4.0) w = 0.5 * (h.L - u);
                u += w;
                s.PlotU[++k] = u;
            }
            s.PlotU[++k] = h.L;
            return k;
        }

        private static double U(ref House h, double x, double z)
        {
            return (x - h.F.OX) * h.F.UX + (z - h.F.OZ) * h.F.UZ;
        }

        private static double W(ref House h, double x, double z)
        {
            return (x - h.F.OX) * h.F.WX + (z - h.F.OZ) * h.F.WZ;
        }

        /// <summary>Clip the footprint to the slab u0 ≤ u ≤ u1 of the front frame (Sutherland-Hodgman, twice) into the
        /// plot polygon arrays at <paramref name="at"/>. Returns the point count.</summary>
        private static int ClipPlot(ref House h, Scratch s, int n, double u0, double u1, int at)
        {
            // Pass 1 into PX/PZ at `at + 2n + 8` (temporary), pass 2 into `at`.
            int tmp = at + 2 * n + 8;
            if (s.PX.Length < tmp + 2 * n + 8)
            {
                Array.Resize(ref s.PX, 2 * (tmp + 2 * n + 8));
                Array.Resize(ref s.PZ, 2 * (tmp + 2 * n + 8));
            }
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double ui = U(ref h, s.X[i], s.Z[i]), uj = U(ref h, s.X[j], s.Z[j]);
                bool ini = ui >= u0, inj = uj >= u0;
                if (ini)
                {
                    s.PX[tmp + k] = s.X[i];
                    s.PZ[tmp + k] = s.Z[i];
                    k++;
                }
                if (ini != inj)
                {
                    double t = (u0 - ui) / (uj - ui);
                    s.PX[tmp + k] = s.X[i] + (s.X[j] - s.X[i]) * t;
                    s.PZ[tmp + k] = s.Z[i] + (s.Z[j] - s.Z[i]) * t;
                    k++;
                }
            }
            int m = 0;
            for (int i = 0; i < k; i++)
            {
                int j = i + 1 == k ? 0 : i + 1;
                double xi = s.PX[tmp + i], zi = s.PZ[tmp + i], xj = s.PX[tmp + j], zj = s.PZ[tmp + j];
                double ui = U(ref h, xi, zi), uj = U(ref h, xj, zj);
                bool ini = ui <= u1, inj = uj <= u1;
                if (ini)
                {
                    s.PX[at + m] = xi;
                    s.PZ[at + m] = zi;
                    m++;
                }
                if (ini != inj)
                {
                    double t = (u1 - ui) / (uj - ui);
                    s.PX[at + m] = xi + (xj - xi) * t;
                    s.PZ[at + m] = zi + (zj - zi) * t;
                    m++;
                }
            }
            // Drop repeated points.
            int o = 0;
            for (int i = 0; i < m; i++)
            {
                double x = s.PX[at + i], z = s.PZ[at + i];
                if (o > 0 && Math.Abs(x - s.PX[at + o - 1]) < 1e-4 && Math.Abs(z - s.PZ[at + o - 1]) < 1e-4) continue;
                s.PX[at + o] = x;
                s.PZ[at + o] = z;
                o++;
            }
            while (o > 1 && Math.Abs(s.PX[at + o - 1] - s.PX[at]) < 1e-4 && Math.Abs(s.PZ[at + o - 1] - s.PZ[at]) < 1e-4) o--;
            return o;
        }

        private static void Measure(ref House h, Scratch s, ref Plot p)
        {
            double maxD = 0, minW = double.MaxValue, maxW = double.MinValue, minU = double.MaxValue, maxU = double.MinValue;
            for (int i = 0; i < p.PolyCount; i++)
            {
                double x = s.PX[p.PolyStart + i], z = s.PZ[p.PolyStart + i];
                double w = W(ref h, x, z), u = U(ref h, x, z);
                if (-w > maxD) maxD = -w;
                if (w < minW) minW = w;
                if (w > maxW) maxW = w;
                minU = Math.Min(minU, u);
                maxU = Math.Max(maxU, u);
            }
            p.Depth = Math.Max(1.0, maxD);
            double area = p.PolyCount >= 3 ? Math.Abs(SignedArea(s.PX, s.PZ, p.PolyStart, p.PolyCount)) : 0;
            // "Rect" enough for a gable over the plot's frame box: most of the box covered, nothing outside it (the
            // roof would leave it open) and nothing in front of the street line.
            p.Rect = p.PolyCount >= 4 && area >= 0.7 * (p.U1 - p.U0) * p.Depth && minU >= p.U0 - 0.3 && maxU <= p.U1 + 0.3 && minW > -p.Depth - 0.01 &&
                     maxW < 0.05;
        }

        private static double SignedArea(double[] x, double[] z, int start, int n)
        {
            double a = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                a += x[start + i] * z[start + j] - x[start + j] * z[start + i];
            }
            return 0.5 * a;
        }

        /// <summary>The plot's archetype, storeys, heights, roof and colours. Plot 0 is the plan; the others draw their
        /// own archetype (40%), storeys (one or two fewer on 45%) and palette, never taller than the plan (the B1
        /// extrusion is the envelope).</summary>
        private static void Configure(ref House h, Scratch s, ref Plot p)
        {
            HousePlan plan = h.Plan;
            StyleParams st = h.Style;
            p.Seed = p.Index == 0 ? plan.Seed : GrammarRng.Mix(plan.Seed, (uint)p.Index * 0x9E37u + 1u);
            var rng = new GrammarRng(p.Seed, PurposePlotArch);
            p.Arch = plan.Archetype;
            p.Storeys = plan.Storeys;
            if (p.Index > 0)
            {
                if (BuildingGrammar.IsNewarProfile(plan.Profile) && rng.Chance(0.4f))
                {
                    float r = rng.Next() * (st.NewarShare + st.HybridShare + st.ModernShare);
                    p.Arch = r < st.NewarShare ? BuildingArchetype.Newar : r < st.NewarShare + st.HybridShare ? BuildingArchetype.NewarHybrid : BuildingArchetype.ModernUrban;
                }
                if (p.Storeys > 2 && rng.Chance(0.45f)) p.Storeys -= p.Storeys >= 5 && rng.Chance(0.4f) ? 2 : 1;
            }
            if (p.Arch == BuildingArchetype.Newar && p.Storeys > 5) p.Arch = BuildingArchetype.NewarHybrid;
            if (p.Arch == BuildingArchetype.Generic) p.Arch = BuildingArchetype.ModernUrban;
            p.Shop = p.Index == 0 ? plan.ShopGround : p.Arch != BuildingArchetype.RanaPalace && p.Storeys >= 2 &&
                                                       rng.Chance(plan.MainLane ? st.ShopMain : st.ShopSide);
            bool newarish = p.Arch == BuildingArchetype.Newar || p.Arch == BuildingArchetype.NewarHybrid;
            p.Plinth = newarish ? 0.15 + 0.3 * ((p.Seed >> 7 & 0xFF) / 255.0) : p.Arch == BuildingArchetype.RanaPalace ? plan.PlinthM : 0.3;
            if (p.Index == 0) p.Plinth = plan.PlinthM;
            int fb = p.Index * MaxFloors;
            double v = plan.MinHeightM + p.Plinth;
            int storeys = Math.Min(p.Storeys, MaxFloors - 2);
            p.Storeys = storeys;
            for (int k = 0; k < storeys; k++)
            {
                s.Floors[fb + k] = v;
                v += plan.StoreyScale * BuildingGrammar.StoreyHeightM(p.Arch, k, p.Shop);
            }
            s.Floors[fb + storeys] = v;
            p.Top = v;
            // Newar houses carry the jhingati gable; hybrids too where the heritage rules ask for sloped tile roofs (the
            // profile's tile share: Bhaktapur 70%, Patan 45%) or the plan says so.
            bool tiled = p.Arch == BuildingArchetype.Newar ||
                         p.Arch == BuildingArchetype.NewarHybrid && (p.Index == 0 ? plan.TileRoof : rng.Chance(st.TileRoofShare));
            p.Gable = tiled && p.Rect && p.Depth <= 13.5 && p.U1 - p.U0 >= 2.5;
            p.Flat = !p.Gable;
            if (p.Index == 0 && plan.Roof != PlanRoof.Flat && plan.Roof != PlanRoof.Gable) p.Flat = true; // tagged odd shapes: a terrace
            p.Parapet = p.Flat ? (p.Arch == BuildingArchetype.ModernUrban ? 1.0 : 0.9) : 0.0;
            p.TileHood = p.Arch == BuildingArchetype.NewarHybrid && rng.Chance(Math.Max(0.35f, st.TileRoofShare));

            // Colours.
            var pal = new GrammarRng(p.Seed, 0x50414C54);
            p.Wood = st.BlackWindowShare > 0 && pal.Chance(st.BlackWindowShare) ? BuildingGrammar.PaintedBlack
                : plan.Profile == StyleProfile.Bungamati && pal.Chance(0.4f) ? BuildingGrammar.SalLight
                : (plan.Profile == StyleProfile.Patan || plan.Profile == StyleProfile.KathmanduCore) && pal.Chance(0.22f) ? BuildingGrammar.PaintedGreen
                : plan.Profile == StyleProfile.KathmanduCore && pal.Chance(0.15f) ? BuildingGrammar.PaintedBrown : BuildingGrammar.SalDark;
            bool heritage = BuildingGrammar.IsNewarProfile(plan.Profile);
            switch (p.Arch)
            {
                case BuildingArchetype.Newar:
                    p.Wall = p.Index == 0 ? plan.Wall : BuildingGrammar.BrickColour(st, ref pal);
                    p.Front = p.Wall;
                    p.WallCh = MaterialChannel.Brick;
                    p.FrontCh = st.CarvedBands || plan.Profile == StyleProfile.Patan ? MaterialChannel.BrickGlazed : MaterialChannel.Brick;
                    if ((plan.Profile == StyleProfile.Bungamati || plan.Profile == StyleProfile.Khokana) && pal.Chance(0.4f))
                    {
                        // Village houses: mud or ochre plaster over the brick, red-painted timber.
                        p.Front = pal.Chance(0.5f) ? BuildingGrammar.MudPlaster : BuildingGrammar.Ochre;
                        p.FrontCh = MaterialChannel.Plaster;
                        if (pal.Chance(0.5f)) p.Wood = BuildingGrammar.PaintedRed;
                    }
                    p.Roof = p.Index == 0 && plan.TileRoof ? plan.RoofColour : BuildingGrammar.JhingatiColour(ref pal);
                    break;
                case BuildingArchetype.NewarHybrid:
                    p.Wall = BuildingGrammar.BrickColour(st, ref pal);
                    p.ExposedBrick = heritage && pal.Chance(0.55f);
                    p.Front = p.ExposedBrick ? p.Wall : BuildingGrammar.ModernPaintColour(plan.Profile, ref pal);
                    p.WallCh = MaterialChannel.Brick;
                    p.FrontCh = p.ExposedBrick ? MaterialChannel.Brick : MaterialChannel.Paint;
                    p.Roof = BuildingGrammar.Concrete;
                    break;
                case BuildingArchetype.RanaPalace:
                    p.Wall = plan.Wall;
                    p.Front = plan.Wall;
                    p.WallCh = MaterialChannel.Plaster;
                    p.FrontCh = MaterialChannel.Plaster;
                    p.Roof = BuildingGrammar.Concrete;
                    break;
                default:
                    if (p.Index == 0)
                    {
                        p.Front = plan.Front;
                        p.Wall = plan.Wall;
                    }
                    else
                    {
                        p.Front = st.GlazedTileShare > 0 && pal.Chance(st.GlazedTileShare) ? BuildingGrammar.Glazed[pal.Int(0, BuildingGrammar.Glazed.Length - 1)]
                            : BuildingGrammar.ModernPaintColour(plan.Profile, ref pal);
                        p.Wall = pal.Chance(0.6f) ? BuildingGrammar.RawBrick : p.Front;
                    }
                    p.Glazed = Array.IndexOf(BuildingGrammar.Glazed, p.Front) >= 0;
                    p.ExposedBrick = p.Front == BuildingGrammar.RawBrick;
                    p.FrontCh = p.Glazed ? MaterialChannel.BrickGlazed : p.ExposedBrick ? MaterialChannel.Brick
                        : p.Front == BuildingGrammar.RawConcrete ? MaterialChannel.Concrete : MaterialChannel.Paint;
                    p.PaintedSides = p.Wall != BuildingGrammar.RawBrick;
                    p.WallCh = p.PaintedSides ? (p.Glazed ? MaterialChannel.Paint : p.FrontCh) : MaterialChannel.Brick;
                    if (p.PaintedSides && p.Glazed) p.Wall = MeshColor.Scale(p.Front, 0.95f);
                    p.Roof = BuildingGrammar.Concrete;
                    break;
            }
            p.Trim = pal.Chance(0.5f) ? MeshColor.FromHex(0xFFFFFF) : MeshColor.Scale(p.Front, 0.8f);
            if (p.Gable && p.Arch != BuildingArchetype.Newar) p.Roof = BuildingGrammar.JhingatiColour(ref pal);
            if (!p.Gable) p.Roof = BuildingGrammar.Concrete; // a flat terrace is concrete, whatever the plan's tile colour
            if (p.Gable)
            {
                double pitch = (h.Style.RoofPitchDeg + (pal.Next() - 0.5f) * 4f) * Math.PI / 180.0;
                p.RidgeV = p.Top + 0.5 * p.Depth * Math.Tan(pitch);
            }
            else p.RidgeV = p.Top + p.Parapet;
        }

        private static double FloorBase(Scratch s, in Plot p, int k)
        {
            if (k < 0) k = 0;
            if (k > p.Storeys) k = p.Storeys;
            return s.Floors[p.Index * MaxFloors + k];
        }

        /// <summary>The display polygon of a plot: its clipped ring with the exposed convex corners rounded (two
        /// segments, smooth normals) and every edge classified (front, body, partition, arc). Returns the point count.</summary>
        private static int Display(ref House h, Scratch s, ref Plot p, in HouseEnv env, int at)
        {
            int n = p.PolyCount, o = 0;
            double r = p.Arch == BuildingArchetype.Newar ? 0.1 : 0.16;
            for (int i = 0; i < n; i++)
            {
                int ip = i == 0 ? n - 1 : i - 1, inx = i + 1 == n ? 0 : i + 1;
                double x = s.PX[p.PolyStart + i], z = s.PZ[p.PolyStart + i];
                double px = s.PX[p.PolyStart + ip], pz = s.PZ[p.PolyStart + ip], nx = s.PX[p.PolyStart + inx], nz = s.PZ[p.PolyStart + inx];
                Edge kin = Classify(ref h, ref p, px, pz, x, z), kout = Classify(ref h, ref p, x, z, nx, nz);
                double dix = x - px, diz = z - pz, li = Math.Sqrt(dix * dix + diz * diz);
                double dox = nx - x, doz = nz - z, lo = Math.Sqrt(dox * dox + doz * doz);
                bool round = r > 0 && h.Det.Segs > 0 && li > 4 * r && lo > 4 * r && kin != Edge.PartitionLow && kin != Edge.PartitionHigh &&
                             kout != Edge.PartitionLow && kout != Edge.PartitionHigh;
                if (round)
                {
                    double cr = dix * doz - diz * dox;
                    round = cr > 0.05 * li * lo; // convex (counter-clockwise ring), not nearly straight
                }
                if (round && env.Neighbours != null)
                {
                    // Something abutting the corner: keep it square (no notch in a continuous row).
                    double bxo = diz / li + doz / lo, bzo = -dix / li - dox / lo, bl = Math.Sqrt(bxo * bxo + bzo * bzo);
                    if (bl > 1e-6 && env.Neighbours.Inside(x + bxo / bl * 0.45, z + bzo / bl * 0.45, env.Index)) round = false;
                    if (round && (env.Neighbours.Inside(x + diz / li * 0.4 + dix / li * 0.3, z - dix / li * 0.4 + diz / li * 0.3, env.Index) ||
                                  env.Neighbours.Inside(x + doz / lo * 0.4 - dox / lo * 0.3, z - dox / lo * 0.4 - doz / lo * 0.3, env.Index)))
                        round = false;
                }
                if (!round)
                {
                    s.DX[at + o] = x;
                    s.DZ[at + o] = z;
                    s.DK[at + o] = (byte)kout;
                    o++;
                    continue;
                }
                double ux = dix / li, uz = diz / li, vx = dox / lo, vz = doz / lo;
                double ax = x - ux * r, az = z - uz * r, bx = x + vx * r, bz = z + vz * r;
                double n0x = uz, n0z = -ux, n2x = vz, n2z = -vx;
                double mx = 0.25 * ax + 0.5 * x + 0.25 * bx, mz = 0.25 * az + 0.5 * z + 0.25 * bz;
                double n1x = n0x + n2x, n1z = n0z + n2z, nl = Math.Sqrt(n1x * n1x + n1z * n1z);
                n1x /= nl;
                n1z /= nl;
                s.DX[at + o] = ax;
                s.DZ[at + o] = az;
                s.DK[at + o] = (byte)Edge.Arc;
                s.NX[at + o] = n0x;
                s.NZ[at + o] = n0z;
                o++;
                s.DX[at + o] = mx;
                s.DZ[at + o] = mz;
                s.DK[at + o] = (byte)Edge.Arc;
                s.NX[at + o] = n1x;
                s.NZ[at + o] = n1z;
                o++;
                s.DX[at + o] = bx;
                s.DZ[at + o] = bz;
                s.DK[at + o] = (byte)kout;
                s.NX[at + o] = n2x;
                s.NZ[at + o] = n2z;
                o++;
            }
            // Facade range: the extent of the front edges.
            p.FU0 = double.MaxValue;
            p.FU1 = double.MinValue;
            for (int i = 0; i < o; i++)
            {
                if (s.DK[at + i] != (byte)Edge.Front) continue;
                int j = i + 1 == o ? 0 : i + 1;
                double ua = U(ref h, s.DX[at + i], s.DZ[at + i]), ub = U(ref h, s.DX[at + j], s.DZ[at + j]);
                p.FU0 = Math.Min(p.FU0, Math.Min(ua, ub));
                p.FU1 = Math.Max(p.FU1, Math.Max(ua, ub));
            }
            if (p.FU0 > p.FU1)
            {
                p.FU0 = p.U0;
                p.FU1 = p.U0; // no front: nothing to dress
            }
            return o;
        }

        private static Edge Classify(ref House h, ref Plot p, double ax, double az, double bx, double bz)
        {
            double ua = U(ref h, ax, az), ub = U(ref h, bx, bz), wa = W(ref h, ax, az), wb = W(ref h, bx, bz);
            if (p.Index > 0 && Math.Abs(ua - p.U0) < 2e-3 && Math.Abs(ub - p.U0) < 2e-3) return Edge.PartitionLow;
            if (p.Index < h.Plots - 1 && Math.Abs(ua - p.U1) < 2e-3 && Math.Abs(ub - p.U1) < 2e-3) return Edge.PartitionHigh;
            if (Math.Abs(wa) < 0.03 && Math.Abs(wb) < 0.03 && ub - ua > 0.05) return Edge.Front;
            return Edge.Body;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Paint
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Walls: dark at the ground contact, open 1.6 m above it; downward faces darker.</summary>
        private static KitPaint WallPaint(ref House h, MaterialChannel ch, float ao)
        {
            return KitPaint.Of(ch, ao).WithGround(h.Ground, 0.5f, 1.6f);
        }

        /// <summary>An element fixed to the facade plane w = 0 of frame f: dark where it meets the wall.</summary>
        private static void Fixed(ref House h, in KitFrame f, MeshData m, int v0, MaterialChannel ch, float ao = 1f)
        {
            KitPaint.Of(ch, ao).WithGround(h.Ground, 0.6f, 1.2f).WithWall(f, 0, 0.62f, 0.22f).Apply(m, v0);
        }

        /// <summary>The inside of an opening of the given depth: open at the wall face, dark at the back.</summary>
        private static void Recess(ref House h, in KitFrame f, MeshData m, int v0, MaterialChannel ch, double depth, float ao = 1f)
        {
            KitPaint.Of(ch, ao).WithWall(f, -depth, 0.45f, (float)Math.Max(0.05, depth)).Apply(m, v0);
        }

        private static void Free(ref House h, MeshData m, int v0, MaterialChannel ch, float ao = 1f)
        {
            KitPaint.Of(ch, ao).WithGround(h.Ground, 0.6f, 1.0f).Apply(m, v0);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Clearance
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>How far an element over [u0, u1] of the facade whose lowest point is <paramref name="v"/> above the
        /// house's ground may project (decision 2): <paramref name="want"/>, or the free depth before a road corridor.</summary>
        private static double Allow(ref House h, in KitFrame f, double u0, double u1, double v, double want)
        {
            if (!h.Clear.Active) return want;
            return h.Clear.Depth(f, u0, u1, 0, h.Ground + v, want, ref h.G);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Body walls
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The plot's non-front walls (and its rounded corners) from the base to the wall top (plus the
        /// parapet), a partition wall only where this plot rises above its neighbour, the hybrid brick-to-plaster seam
        /// and the plinth course.</summary>
        private static void BodyWalls(ref House h, Scratch s, ref Plot p, MeshData m)
        {
            int at = p.DispStart, n = p.DispCount;
            double top = h.Ground + p.Top + p.Parapet, bottom = h.Base;
            double seam = p.Arch == BuildingArchetype.NewarHybrid && p.Storeys > 3 ? h.Ground + FloorBase(s, p, 3) : double.NaN;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                var kind = (Edge)s.DK[at + i];
                double ax = s.DX[at + i], az = s.DZ[at + i], bx = s.DX[at + j], bz = s.DZ[at + j];
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-3) continue;
                if (kind == Edge.Front) continue;
                bool frontArc = kind == Edge.Arc && (Touches(s, at, n, i, Edge.Front));
                uint col = frontArc ? p.Front : p.Wall;
                MaterialChannel ch = frontArc ? p.FrontCh : p.WallCh;
                int v0 = m.VertexCount;
                if (kind == Edge.PartitionLow || kind == Edge.PartitionHigh)
                {
                    int nb = kind == Edge.PartitionLow ? p.Index - 1 : p.Index + 1;
                    Plot q = s.Plots[nb];
                    // Only the part above the neighbour (its eave plate or its parapet) shows; a gable's end triangle above
                    // the eave plate is drawn by the roof.
                    double from = h.Ground + Math.Max(0, q.Gable ? q.Top : q.Top + q.Parapet);
                    if (top <= from + 1e-3) continue;
                    MeshKit.Quad(m, ax, from, az, bx, from, bz, bx, top, bz, ax, top, az, dz, 0, -dx, col);
                    WallPaint(ref h, ch, 1f).WithGround(from, 0.55f, 0.8f).Apply(m, v0);
                    continue;
                }
                if (kind == Edge.Arc)
                {
                    double n0x = s.NX[at + i], n0z = s.NZ[at + i], n1x = s.NX[at + j], n1z = s.NZ[at + j];
                    if (!double.IsNaN(seam) && !frontArc)
                    {
                        KitRound.QuadSmooth(m, ax, bottom, az, bx, bottom, bz, bx, seam, bz, ax, seam, az, n0x, 0, n0z, n1x, 0, n1z, n1x, 0, n1z, n0x, 0, n0z, col);
                        WallPaint(ref h, ch, 1f).Apply(m, v0);
                        v0 = m.VertexCount;
                        KitRound.QuadSmooth(m, ax, seam, az, bx, seam, bz, bx, top, bz, ax, top, az, n0x, 0, n0z, n1x, 0, n1z, n1x, 0, n1z, n0x, 0, n0z, p.Front);
                        WallPaint(ref h, p.FrontCh == MaterialChannel.Brick ? MaterialChannel.Plaster : p.FrontCh, 1f).Apply(m, v0);
                        continue;
                    }
                    KitRound.QuadSmooth(m, ax, bottom, az, bx, bottom, bz, bx, top, bz, ax, top, az, n0x, 0, n0z, n1x, 0, n1z, n1x, 0, n1z, n0x, 0, n0z, col);
                    WallPaint(ref h, ch, 1f).Apply(m, v0);
                    continue;
                }
                bool street = h.Corner && OnSegment(ax, az, bx, bz, h.SecondAX, h.SecondAZ, h.SecondBX, h.SecondBZ) || FacesStreet(ref h, ax, az, bx, bz, len);
                if (street && SideFacade(ref h, s, ref p, ax, az, bx, bz, m)) continue;
                if (!double.IsNaN(seam))
                {
                    MeshKit.Quad(m, ax, bottom, az, bx, bottom, bz, bx, seam, bz, ax, seam, az, dz, 0, -dx, col);
                    WallPaint(ref h, ch, 1f).Apply(m, v0);
                    v0 = m.VertexCount;
                    uint upper = p.ExposedBrick ? MeshColor.Scale(p.Wall, 1.06f) : MeshColor.Lerp(p.Front, BuildingGrammar.Concrete, 0.35f);
                    MeshKit.Quad(m, ax, seam, az, bx, seam, bz, bx, top, bz, ax, top, az, dz, 0, -dx, upper);
                    WallPaint(ref h, p.ExposedBrick ? MaterialChannel.Brick : MaterialChannel.Plaster, 1f).Apply(m, v0);
                }
                else
                {
                    MeshKit.Quad(m, ax, bottom, az, bx, bottom, bz, bx, top, bz, ax, top, az, dz, 0, -dx, col);
                    WallPaint(ref h, ch, 1f).Apply(m, v0);
                }
                // Plinth course along the exposed straight walls.
                if (len > 0.6 && p.Plinth > 0.1)
                {
                    var f = new KitFrame(ax, h.Ground, az, dx, dz);
                    v0 = m.VertexCount;
                    FacadeKit.Ledge(m, f, 0, len, h.Base - h.Ground, p.Plinth - (h.Base - h.Ground) + 0.05, 0.05, 0.03, 0, PlinthColour(ref h, ref p));
                    Free(ref h, m, v0, PlinthChannel(ref p));
                }
            }
        }

        /// <summary>True when a body wall looks onto a street: a road corridor within 4 m in front of its middle and
        /// no other building against it (walls on lanes get windows and shops, not blank brick).</summary>
        private static bool FacesStreet(ref House h, double ax, double az, double bx, double bz, double len)
        {
            if (!h.Clear.Active || len < 2.5) return false;
            double nx = (bz - az) / len, nz = -(bx - ax) / len;
            for (int k = 1; k <= 3; k++)
            {
                double t = 0.25 * k, mx = ax + (bx - ax) * t, mz = az + (bz - az) * t;
                if (h.Neighbours != null && h.Neighbours.Inside(mx + nx * 0.8, mz + nz * 0.8, h.Index)) continue;
                if (h.Clear.FreeAt(mx + nx * 0.3, mz + nz * 0.3) < 4.0) return true;
            }
            return false;
        }

        private static bool Touches(Scratch s, int at, int n, int i, Edge kind)
        {
            // Walk the arc run both ways to the first non-arc edge.
            for (int k = 1; k < 4; k++)
            {
                int j = (i - k + n) % n;
                if ((Edge)s.DK[at + j] != Edge.Arc) return (Edge)s.DK[at + j] == kind || NextIs(s, at, n, i, kind);
            }
            return NextIs(s, at, n, i, kind);
        }

        private static bool NextIs(Scratch s, int at, int n, int i, Edge kind)
        {
            for (int k = 1; k < 4; k++)
            {
                int j = (i + k) % n;
                if ((Edge)s.DK[at + j] != Edge.Arc) return (Edge)s.DK[at + j] == kind;
            }
            return false;
        }

        private static bool OnSegment(double ax, double az, double bx, double bz, double sx, double sz, double ex, double ez)
        {
            double d1 = Plane2.PointSeg(ax, az, sx, sz, ex, ez), d2 = Plane2.PointSeg(bx, bz, sx, sz, ex, ez);
            return d1 < 0.05 && d2 < 0.05;
        }

        private static uint PlinthColour(ref House h, ref Plot p)
        {
            switch (p.Arch)
            {
                case BuildingArchetype.Newar:
                case BuildingArchetype.NewarHybrid:
                    return h.Plan.Profile == StyleProfile.Kirtipur || h.Plan.Profile == StyleProfile.Bhaktapur ? BuildingGrammar.PlinthStone : MeshColor.Scale(p.Wall, 0.8f);
                case BuildingArchetype.RanaPalace:
                    return BuildingGrammar.RanaShadow;
                default:
                    return MeshColor.Scale(BuildingGrammar.Concrete, 0.88f);
            }
        }

        private static MaterialChannel PlinthChannel(ref Plot p)
        {
            switch (p.Arch)
            {
                case BuildingArchetype.Newar:
                case BuildingArchetype.NewarHybrid: return MaterialChannel.Stone;
                case BuildingArchetype.RanaPalace: return MaterialChannel.Plaster;
                default: return MaterialChannel.Concrete;
            }
        }
    }
}
