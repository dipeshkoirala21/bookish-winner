using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The B0 facade and roof grammar for one house (W2_DESIGN 2.3, 2.6): NEWAR (house door or shop dalan, tikijhya,
    /// sanjhya, gajhya, floor bands, a jhingati gable parallel to the street on struts, the pikha apron), NEWAR_HYBRID
    /// (Newar floors G-2, plain upper floors, an eave hood, a flat terrace), MODERN_URBAN (columns, slab bands, windows
    /// with chhajja, balconies, shopfronts and signs, raw-brick sides) and RANA_PALACE (tall French windows, cornice,
    /// balustrade). Flat roofs carry the parapet, stair cabin and the seeded roof props (tanks, solar racks, rebar
    /// stubs, dishes, terrace umbrellas). Plot split on long fronts (KTM core, Patan, Thamel, Kirtipur). The drop level
    /// removes detail in the §2.4 order: 1 lattice relief, 2 struts (a fascia stripe instead), 3 floor bands, 4 roof
    /// props; the sanjhya is never dropped.
    /// </summary>
    internal static class HouseBuilder
    {
        public const int MaxDrop = 4;
        private const uint PurposePlots = 0x504C4F54;
        private const uint PurposeFacade = 0x46414344;
        private const uint PurposeProps = 0x50524F50;

        private sealed class Scratch
        {
            public double[] X = new double[64], Z = new double[64];
            public int[] Tris = new int[192], Next = new int[64], Prev = new int[64];
            public double[] PlotU = new double[16];

            public void Ensure(int n)
            {
                if (X.Length >= n) return;
                int cap = Math.Max(n, X.Length * 2);
                X = new double[cap];
                Z = new double[cap];
                Tris = new int[cap * 3];
                Next = new int[cap];
                Prev = new int[cap];
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        /// <summary>Everything the facade code needs about one house.</summary>
        private struct House
        {
            public HousePlan Plan;
            public StyleParams Style;
            public KitFrame F;
            public double L, D;
            public double Ground, Base, Top;
            public int Drop;
            public bool Rect;
            public GenColliders Colliders;
            public double CX, CZ, AreaM2;
        }

        /// <summary>Build one house at a drop level; returns false when the footprint is degenerate.</summary>
        public static bool Build(BuildingRecord b, in HousePlan plan, ref RoadSurface g, float sinkM, int drop, MeshData m, GenColliders c)
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
                Plan = plan, Style = BuildingGrammar.For(plan.Profile), F = new KitFrame(ax, ground, az, bx - ax, bz - az),
                Ground = ground, Drop = drop, Colliders = c, CX = cxm, CZ = czm, AreaM2 = area,
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
            h.Rect = n <= 6 && area >= 0.82 * h.L * h.D;
            h.Base = plan.MinHeightM > 0 ? ground + plan.MinHeightM : ground - sinkM;
            h.Top = ground + plan.WallTopM;

            // Body walls: every edge but the front (the facade draws it), from the base to the wall top; flat roofs carry
            // a parapet on top of the walls.
            bool flat = plan.Roof == PlanRoof.Flat || !h.Rect && plan.Roof == PlanRoof.Gable;
            double parapet = flat ? (plan.Archetype == BuildingArchetype.ModernUrban ? 1.0 : 0.9) : 0.0;
            for (int i = 0; i < n; i++)
            {
                if (i == fe) continue;
                int j = i + 1 == n ? 0 : i + 1;
                double dx = s.X[j] - s.X[i], dz = s.Z[j] - s.Z[i];
                if (dx * dx + dz * dz < 1e-6) continue;
                uint col = i == plan.SecondEdge ? plan.Front : plan.Wall;
                MeshKit.Quad(m, s.X[i], h.Base, s.Z[i], s.X[j], h.Base, s.Z[j], s.X[j], h.Top + parapet, s.Z[j], s.X[i], h.Top + parapet, s.Z[i],
                             dz, 0, -dx, col);
            }

            int plots = Plots(ref h, s);
            for (int p = 0; p < plots; p++)
            {
                double u0 = s.PlotU[p], u1 = s.PlotU[p + 1];
                var prng = new GrammarRng(GrammarRng.Mix(plan.Seed, (uint)p), PurposeFacade);
                uint front = p == 0 ? plan.Front : PlotColour(ref h, ref prng);
                FrontWall(ref h, u0, u1, front, parapet, m);
                switch (plan.Archetype)
                {
                    case BuildingArchetype.Newar:
                        NewarPlot(ref h, ref prng, u0, u1, plan.Storeys, front, m);
                        break;
                    case BuildingArchetype.NewarHybrid:
                        NewarPlot(ref h, ref prng, u0, u1, Math.Min(3, plan.Storeys), front, m);
                        HybridUpper(ref h, ref prng, u0, u1, front, m);
                        break;
                    case BuildingArchetype.RanaPalace:
                        RanaFront(ref h, u0, u1, m);
                        break;
                    default:
                        ModernPlot(ref h, ref prng, u0, u1, front, m);
                        break;
                }
            }

            // Roof.
            if (!flat && plan.Roof == PlanRoof.Gable) NewarRoof(ref h, s, plots, m);
            else if (flat) FlatRoof(ref h, s, n, parapet, m);
            else
            {
                // Hip, skillion, pyramid and other tagged shapes on houses: a pyramid cap over the footprint.
                MeshKit.Loft(m, s.X, s.Z, n, cxm, czm, h.Top, 1.0, h.Top + Math.Max(0.8, plan.RoofRiseM), Polygon.IsConvex(s.X, s.Z, n) ? 0.0 : 0.6,
                             plan.RoofColour);
            }
            return true;
        }

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
                    if (s.X[i] == x && s.Z[i] == z) return i;
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

        /// <summary>Plot boundaries along the front (W2_DESIGN 2.3 plot split): fronts over 9 m in the split profiles
        /// become 4-8 m plots.</summary>
        private static int Plots(ref House h, Scratch s)
        {
            s.PlotU[0] = 0;
            if (!h.Style.PlotSplit || h.L <= 9.0 || !BuildingGrammar.IsHouse(h.Plan.Archetype) || h.Plan.Archetype == BuildingArchetype.RanaPalace)
            {
                s.PlotU[1] = h.L;
                return 1;
            }
            var rng = new GrammarRng(h.Plan.Seed, PurposePlots);
            int k = 0;
            double u = 0;
            while (h.L - u > 8.0 && k < s.PlotU.Length - 2)
            {
                double w = rng.Range(4f, 8f);
                if (h.L - (u + w) < 4.0) w = 0.5 * (h.L - u);
                u += w;
                s.PlotU[++k] = u;
            }
            s.PlotU[++k] = h.L;
            return k;
        }

        private static uint PlotColour(ref House h, ref GrammarRng rng)
        {
            switch (h.Plan.Archetype)
            {
                case BuildingArchetype.Newar: return BuildingGrammar.BrickColour(h.Style, ref rng);
                case BuildingArchetype.NewarHybrid: return rng.Chance(0.5f) ? BuildingGrammar.BrickColour(h.Style, ref rng) : BuildingGrammar.ModernPaintColour(ref rng);
                default: return BuildingGrammar.ModernPaintColour(ref rng);
            }
        }

        private static void FrontWall(ref House h, double u0, double u1, uint c, double parapet, MeshData m)
        {
            MeshKit.QuadLocal(m, h.F, u0, h.Base - h.Ground, 0, u1, h.Base - h.Ground, 0, u1, h.Top - h.Ground + parapet, 0,
                              u0, h.Top - h.Ground + parapet, 0, 0, 0, 1, c);
        }

        private static double Floor(ref House h, int k)
        {
            return h.Plan.FloorBase(k);
        }

        private static double StoreyH(ref House h, int k)
        {
            return h.Plan.StoreyScale * BuildingGrammar.StoreyHeightM(h.Plan.Archetype, k, h.Plan.ShopGround);
        }

        /// <summary>Bay count: <c>clamp(round(width / 1.6), 1, 7)</c>, odd from 4.5 m (a centre bay).</summary>
        private static int Bays(double width)
        {
            int n = (int)Math.Round(width / 1.6);
            n = n < 1 ? 1 : n > 7 ? 7 : n;
            if (width >= 4.5 && n % 2 == 0) n = n == 7 ? 7 : n + 1 > 7 ? n - 1 : n + 1;
            return n;
        }

        // ---------------------------------------------------------------------------------------------------------
        // NEWAR
        // ---------------------------------------------------------------------------------------------------------

        private static void NewarPlot(ref House h, ref GrammarRng rng, double u0, double u1, int floors, uint wall, MeshData m)
        {
            KitFrame f = h.F;
            double w = u1 - u0;
            int bays = Bays(w);
            double bayW = w / bays;
            uint wood = h.Plan.Wood;
            uint lattice = MeshColor.Scale(BuildingGrammar.SalMid, 0.85f);
            uint band = h.Style.CarvedBands ? BuildingGrammar.SalMid : (h.Style.HasBrick ? h.Style.BrickJoint : MeshColor.Scale(wall, 0.7f));

            // G: shop dalan or house door, on a pikha apron.
            double g0 = Floor(ref h, 0), gH = StoreyH(ref h, 0);
            bool shop = h.Plan.ShopGround && (h.Plan.Archetype == BuildingArchetype.Newar || h.Plan.Archetype == BuildingArchetype.NewarHybrid);
            if (shop)
            {
                int posts = Math.Max(2, Math.Min(4, (int)Math.Round(w / 1.5) + 1));
                uint shutter = BuildingGrammar.Shutter[rng.Int(0, BuildingGrammar.Shutter.Length - 1)];
                double sh = Math.Min(gH - 0.1, 2.4);
                MeshKit.Panel(m, f, u0 + 0.1, g0, u1 - 0.1, g0 + sh, 0.02, shutter);
                for (int k = 0; k < posts; k++)
                {
                    double u = u0 + 0.1 + (w - 0.32) * k / (posts - 1);
                    MeshKit.Box(m, f, u, u + 0.12, g0, g0 + sh, 0, 0.12, wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                Sign(ref h, ref rng, u0 + 0.2, u1 - 0.2, g0 + sh, m);
            }
            else
            {
                double uc = u0 + 0.5 * w, dw = rng.Range(0.75f, 0.9f), dh = rng.Range(1.45f, 1.7f);
                MeshKit.Box(m, f, uc - 0.5 * dw - 0.12, uc + 0.5 * dw + 0.12, g0, g0 + dh + 0.12, 0, 0.06, wood, BoxFaces.Wall);
                MeshKit.Panel(m, f, uc - 0.5 * dw, g0, uc + 0.5 * dw, g0 + dh, 0.062, MeshColor.FromHex(0x2A1A12));
                MeshKit.Box(m, f, uc - 0.5 * dw - 0.42, uc + 0.5 * dw + 0.42, g0 + dh + 0.12, g0 + dh + 0.24, 0, 0.09, wood, BoxFaces.Wall);
                MeshKit.Box(m, f, uc - 0.15, uc + 0.15, g0 + dh + 0.3, g0 + dh + 0.55, 0, 0.04, BuildingGrammar.Sindoor, BoxFaces.Front | BoxFaces.Top);
                for (int k = 0; k < bays; k++)
                {
                    double bc = u0 + (k + 0.5) * bayW;
                    if (Math.Abs(bc - uc) < 0.6 * bayW) continue;
                    MeshKit.Panel(m, f, bc - 0.25, g0 + 1.0, bc + 0.25, g0 + 1.6, 0.01, wood);
                }
                // Pikha apron.
                double ph = Math.Max(0.3, h.Plan.PlinthM + 0.15);
                MeshKit.Box(m, f, u0, u1, h.Base - h.Ground, ph, 0, 0.6, BuildingGrammar.PlinthStone, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                if (h.Colliders != null)
                {
                    double cx, cy, cz;
                    f.ToWorld(0.5 * (u0 + u1), 0, 0.3, out cx, out cy, out cz);
                    h.Colliders.AddBox(cx, cz, h.Base, h.Ground + ph, 0.5 * w, 0.3, f.UX, f.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
                }
            }

            for (int k = 1; k < floors && k < h.Plan.Storeys; k++)
            {
                double b0 = Floor(ref h, k), sH = StoreyH(ref h, k);
                if (h.Drop < 3) MeshKit.Box(m, f, u0, u1, b0 - 0.12, b0 + 0.12, 0, 0.18, band, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                bool attic = k == h.Plan.Storeys - 1 && k >= 3 && h.Plan.Archetype == BuildingArchetype.Newar;
                double uc = u0 + 0.5 * w;
                if (k == 2 || (k > 2 && !attic && h.Plan.Archetype == BuildingArchetype.Newar && rng.Chance(0.3f)))
                {
                    // Sanjhya in the centre bay (a long sanjhya on 15% of wide fronts); never dropped.
                    double sw = w >= 7 && rng.Chance(0.15f) ? w - 0.6 : w >= 4.5 ? Math.Min(rng.Range(2.4f, 3.6f), w - 0.6) : Math.Min(1.2, w - 0.4);
                    Sanjhya(ref h, uc, sw, b0 + 0.3, Math.Min(1.35, sH - 0.5), rng.Range(0.3f, 0.6f), wood, lattice, rng.Chance(0.1f), m);
                    for (int bay = 0; bay < bays; bay++)
                    {
                        double bc = u0 + (bay + 0.5) * bayW;
                        if (Math.Abs(bc - uc) < 0.5 * sw + 0.4) continue;
                        Tikijhya(ref h, bc, b0 + 0.4, Math.Min(bayW - 0.3, 0.75), Math.Min(0.95, sH - 0.7), wood, lattice, m);
                    }
                }
                else if (attic)
                {
                    // Gajhya under the eave.
                    double gw = Math.Min(1.2, w - 0.4), gh = Math.Min(0.7, sH - 0.4);
                    MeshKit.Box(m, f, uc - 0.5 * gw - 0.1, uc + 0.5 * gw + 0.1, b0 + 0.2, b0 + 0.3 + gh, 0, 0.4, wood, BoxFaces.Wall | BoxFaces.Bottom);
                    MeshKit.Panel(m, f, uc - 0.5 * gw, b0 + 0.3, uc + 0.5 * gw, b0 + 0.2 + gh, 0.405, lattice);
                }
                else
                {
                    for (int bay = 0; bay < bays; bay++)
                        Tikijhya(ref h, u0 + (bay + 0.5) * bayW, b0 + 0.4, Math.Min(bayW - 0.3, 0.75), Math.Min(0.95, sH - 0.7), wood, lattice, m);
                }
            }
        }

        /// <summary>Tikijhya: a framed lattice window (opaque inset) with lintel ears and, above drop level 1, a
        /// lattice relief.</summary>
        private static void Tikijhya(ref House h, double uc, double sill, double ww, double wh, uint wood, uint lattice, MeshData m)
        {
            if (ww < 0.3 || wh < 0.3) return;
            KitFrame f = h.F;
            MeshKit.Box(m, f, uc - 0.5 * ww - 0.15, uc + 0.5 * ww + 0.15, sill - 0.15, sill + wh + 0.12, 0, 0.08, wood, BoxFaces.Wall);
            MeshKit.Panel(m, f, uc - 0.5 * ww, sill, uc + 0.5 * ww, sill + wh, 0.081, lattice);
            if (h.Drop < 1)
            {
                double x0, y0, z0, x1, y1, z1;
                f.ToWorld(uc - 0.5 * ww, sill, 0.1, out x0, out y0, out z0);
                f.ToWorld(uc + 0.5 * ww, sill + wh, 0.1, out x1, out y1, out z1);
                MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.04, wood);
                f.ToWorld(uc + 0.5 * ww, sill, 0.1, out x0, out y0, out z0);
                f.ToWorld(uc - 0.5 * ww, sill + wh, 0.1, out x1, out y1, out z1);
                MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.04, wood);
            }
        }

        /// <summary>Sanjhya: a projecting bay window on two brackets with a lattice face and a small sloped hood.</summary>
        private static void Sanjhya(ref House h, double uc, double sw, double v0, double sh, double proj, uint wood, uint lattice, bool marigold, MeshData m)
        {
            if (sw < 0.6 || sh < 0.5) return;
            KitFrame f = h.F;
            MeshKit.Box(m, f, uc - 0.5 * sw, uc + 0.5 * sw, v0, v0 + sh, 0, proj, wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            MeshKit.Panel(m, f, uc - 0.5 * sw + 0.12, v0 + 0.1, uc + 0.5 * sw - 0.12, v0 + sh - 0.1, proj + 0.005, lattice);
            MeshKit.QuadLocal(m, f, uc - 0.5 * sw - 0.1, v0 + sh, proj + 0.15, uc + 0.5 * sw + 0.1, v0 + sh, proj + 0.15,
                              uc + 0.5 * sw + 0.1, v0 + sh + 0.3, 0, uc - 0.5 * sw - 0.1, v0 + sh + 0.3, 0, 0, 1, 0.5, BuildingGrammar.SalDark);
            MeshKit.Box(m, f, uc - 0.5 * sw + 0.1, uc - 0.5 * sw + 0.25, v0 - 0.35, v0, 0, proj * 0.8, wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            MeshKit.Box(m, f, uc + 0.5 * sw - 0.25, uc + 0.5 * sw - 0.1, v0 - 0.35, v0, 0, proj * 0.8, wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            if (marigold && h.Drop < 4) MeshKit.Box(m, f, uc - 0.2, uc + 0.2, v0 - 0.02, v0 + 0.22, proj, proj + 0.25, BuildingGrammar.Marigold, BoxFaces.Wall);
        }

        /// <summary>The jhingati gable with its ridge parallel to the street, one segment per plot group (eave steps of
        /// 0.2-0.6 m between groups of 2-4 plots), overhanging front and back, on struts (or a fascia stripe).</summary>
        private static void NewarRoof(ref House h, Scratch s, int plots, MeshData m)
        {
            KitFrame f = h.F;
            double pitch = h.Style.RoofPitchDeg * Math.PI / 180.0, tan = Math.Tan(pitch);
            double ovr = h.Style.EaveOverhangM;
            var rng = new GrammarRng(h.Plan.Seed, PurposePlots ^ 0x5A5A);
            double top = h.Top - h.Ground;
            int p = 0;
            double step = 0;
            while (p < plots)
            {
                int group = Math.Min(plots - p, rng.Int(2, 4));
                double u0 = s.PlotU[p], u1 = s.PlotU[p + group];
                double t = top + step;
                uint tile = BuildingGrammar.JhingatiColour(ref rng);
                if (p == 0) tile = h.Plan.RoofColour;
                double ext0 = p == 0 ? 0.3 : 0, ext1 = p + group == plots ? 0.3 : 0;
                double ridgeW = -0.5 * h.D, ridgeV = t + 0.5 * h.D * tan;
                double eaveF = t - ovr * tan, eaveB = t - ovr * tan;
                double a = u0 - ext0, b = u1 + ext1;
                // Front and back planes.
                MeshKit.QuadLocal(m, f, a, eaveF, ovr, b, eaveF, ovr, b, ridgeV, ridgeW, a, ridgeV, ridgeW, 0, 1, 1, tile);
                MeshKit.QuadLocal(m, f, a, eaveB, -h.D - ovr, b, eaveB, -h.D - ovr, b, ridgeV, ridgeW, a, ridgeV, ridgeW, 0, 1, -1, tile);
                // Undersides of the overhangs (seen from the street).
                uint under = MeshColor.Scale(BuildingGrammar.SalDark, 1.1f);
                MeshKit.QuadLocal(m, f, a, eaveF, ovr, b, eaveF, ovr, b, t, 0, a, t, 0, 0, -1, 0, under);
                MeshKit.QuadLocal(m, f, a, eaveB, -h.D - ovr, b, eaveB, -h.D - ovr, b, t, -h.D, a, t, -h.D, 0, -1, 0, under);
                // Ridge cap.
                MeshKit.Box(m, f, a, b, ridgeV - 0.05, ridgeV + 0.12, ridgeW - 0.12, ridgeW + 0.12, BuildingGrammar.JhingatiRidge, BoxFaces.Top | BoxFaces.Front | BoxFaces.Back);
                // Gable ends (wall colour): visible at the row ends and at eave steps.
                MeshKit.TriLocal(m, f, u0, t, 0, u0, t, -h.D, u0, ridgeV, ridgeW, -1, 0, 0, h.Plan.Wall);
                MeshKit.TriLocal(m, f, u1, t, 0, u1, t, -h.D, u1, ridgeV, ridgeW, 1, 0, 0, h.Plan.Wall);
                if (step > 0)
                {
                    // Raise the walls of a stepped group to its eave.
                    MeshKit.QuadLocal(m, f, u0, top, 0, u1, top, 0, u1, t, 0, u0, t, 0, 0, 0, 1, h.Plan.Front);
                    MeshKit.QuadLocal(m, f, u0, top, -h.D, u1, top, -h.D, u1, t, -h.D, u0, t, -h.D, 0, 0, -1, h.Plan.Wall);
                }
                // Struts along the front eave, plain on houses; a fascia stripe from drop level 2.
                if (h.Drop < 2)
                {
                    double spacing = rng.Range(1.2f, 1.8f);
                    int count = Math.Max(1, (int)Math.Floor((u1 - u0) / spacing));
                    for (int k = 0; k < count; k++)
                    {
                        double u = u0 + (k + 0.5) * (u1 - u0) / count;
                        double x0, y0, z0, x1, y1, z1;
                        f.ToWorld(u, t - 1.15, 0.06, out x0, out y0, out z0);
                        f.ToWorld(u, eaveF + 0.12, ovr - 0.15, out x1, out y1, out z1);
                        MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.12, h.Plan.Wood);
                    }
                }
                else
                {
                    MeshKit.Panel(m, f, u0, t - 0.35, u1, t, 0.01, h.Plan.Wood);
                }
                p += group;
                step = rng.Range(0.2f, 0.6f) * (rng.Chance(0.5f) ? 1 : 0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // NEWAR_HYBRID upper floors
        // ---------------------------------------------------------------------------------------------------------

        private static void HybridUpper(ref House h, ref GrammarRng rng, double u0, double u1, uint front, MeshData m)
        {
            KitFrame f = h.F;
            double w = u1 - u0;
            if (h.Plan.Storeys > 3)
            {
                // The single-slope eave hood at the old eave line, on 2-4 struts.
                double v = Floor(ref h, 3), depth = rng.Range(0.6f, 0.9f), drop = depth * Math.Tan(27 * Math.PI / 180);
                uint hood = h.Plan.TileRoof ? h.Plan.RoofColour : MeshColor.FromHex(0x3D7CC9);
                MeshKit.QuadLocal(m, f, u0, v - drop, depth, u1, v - drop, depth, u1, v, 0, u0, v, 0, 0, 1, 1, hood);
                MeshKit.QuadLocal(m, f, u0, v - drop, depth, u1, v - drop, depth, u1, v, 0, u0, v, 0, 0, -1, -1, BuildingGrammar.SalDark);
                if (h.Drop < 2)
                {
                    int struts = Math.Max(2, Math.Min(4, (int)Math.Round(w / 1.6)));
                    for (int k = 0; k < struts; k++)
                    {
                        double u = u0 + (k + 0.5) * w / struts, x0, y0, z0, x1, y1, z1;
                        f.ToWorld(u, v - 0.9, 0.05, out x0, out y0, out z0);
                        f.ToWorld(u, v - drop, depth - 0.1, out x1, out y1, out z1);
                        MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.1, h.Plan.Wood);
                    }
                }
            }
            int bays = Math.Max(1, Bays(w) / 2 + 1);
            for (int k = 3; k < h.Plan.Storeys; k++)
            {
                double b0 = Floor(ref h, k);
                if (h.Drop < 3) MeshKit.Box(m, f, u0, u1, b0 - 0.08, b0 + 0.08, 0, 0.1, MeshColor.Scale(front, 0.85f), BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                for (int bay = 0; bay < bays; bay++)
                {
                    double uc = u0 + (bay + 0.5) * w / bays;
                    MeshKit.Panel(m, f, uc - 0.4, b0 + 0.75, uc + 0.4, b0 + 2.15, 0.01, BuildingGrammar.Glass);
                    MeshKit.Box(m, f, uc - 0.5, uc + 0.5, b0 + 0.68, b0 + 0.75, 0, 0.08, h.Plan.Trim, BoxFaces.Front | BoxFaces.Top);
                }
                if (k == h.Plan.Storeys - 1 && rng.Chance(0.3f)) Balcony(ref h, ref rng, u0 + 0.5 * w, Math.Min(2.4, w - 0.6), b0, m);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // MODERN_URBAN
        // ---------------------------------------------------------------------------------------------------------

        private static void ModernPlot(ref House h, ref GrammarRng rng, double u0, double u1, uint front, MeshData m)
        {
            KitFrame f = h.F;
            double w = u1 - u0;
            uint trim = h.Plan.Trim;
            double g0 = Floor(ref h, 0), gH = StoreyH(ref h, 0);
            // Ground floor: shopfront with shutters and a sign, or a door and windows.
            if (h.Plan.ShopGround)
            {
                int bays = Math.Max(1, (int)Math.Round(w / 2.6));
                uint shutter = BuildingGrammar.Shutter[rng.Int(0, BuildingGrammar.Shutter.Length - 1)];
                double sh = Math.Min(gH - 0.4, 2.7);
                for (int k = 0; k < bays; k++)
                {
                    double a = u0 + w * k / bays + 0.12, b = u0 + w * (k + 1) / bays - 0.12;
                    MeshKit.Panel(m, f, a, g0, b, g0 + sh, 0.02, shutter);
                }
                Sign(ref h, ref rng, u0 + 0.15, u1 - 0.15, g0 + sh, m);
            }
            else
            {
                double uc = u0 + 0.5 * w;
                MeshKit.Panel(m, f, uc - 0.5, g0, uc + 0.5, g0 + 2.1, 0.02, MeshColor.FromHex(0x5A3A28));
                if (w > 4)
                {
                    MeshKit.Panel(m, f, u0 + 0.5, g0 + 0.9, u0 + 1.7, g0 + 2.2, 0.01, BuildingGrammar.Glass);
                    MeshKit.Panel(m, f, u1 - 1.7, g0 + 0.9, u1 - 0.5, g0 + 2.2, 0.01, BuildingGrammar.Glass);
                }
            }
            // Columns at the plot edges and every 3-4.5 m.
            int cols = Math.Max(2, (int)Math.Round(w / 3.8) + 1);
            for (int k = 0; k < cols; k++)
            {
                double u = u0 + (w - 0.25) * k / (cols - 1);
                MeshKit.Box(m, f, u, u + 0.25, g0, h.Top - h.Ground, 0, 0.06, MeshColor.Scale(front, 0.92f), BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            bool balconies = rng.Chance(0.5f) && w >= 4.5;
            int windows = Math.Max(1, Math.Min(4, (int)Math.Round(w / 2.8)));
            for (int k = 1; k < h.Plan.Storeys; k++)
            {
                double b0 = Floor(ref h, k);
                if (h.Drop < 3) MeshKit.Box(m, f, u0, u1, b0 - 0.15, b0, 0, 0.08, trim, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                double uc = u0 + 0.5 * w;
                if (balconies) Balcony(ref h, ref rng, uc, Math.Min(2.6, w * 0.4), b0, m);
                for (int j = 0; j < windows; j++)
                {
                    double c = u0 + (j + 0.5) * w / windows;
                    if (balconies && Math.Abs(c - uc) < 1.4) continue;
                    MeshKit.Panel(m, f, c - 0.6, b0 + 0.9, c + 0.6, b0 + 2.25, 0.01, BuildingGrammar.Glass);
                    MeshKit.Box(m, f, c - 0.75, c + 0.75, b0 + 2.35, b0 + 2.45, 0, 0.4, trim, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                }
                // Thamel: vertical blade signs on the upper floors.
                if (h.Style.SignsMax > 1 && k <= 3 && rng.Chance(0.4f))
                {
                    double u = rng.Chance(0.5f) ? u0 + 0.3 : u1 - 0.35;
                    uint col = BuildingGrammar.Sign[rng.Int(0, BuildingGrammar.Sign.Length - 1)];
                    MeshKit.Box(m, f, u, u + 0.06, b0 + 0.3, b0 + 2.3, 0.1, 0.1 + rng.Range(0.4f, 0.6f), col, BoxFaces.All & ~BoxFaces.Back);
                }
            }
        }

        private static void Balcony(ref House h, ref GrammarRng rng, double uc, double bw, double floorBase, MeshData m)
        {
            if (bw < 1.0) return;
            KitFrame f = h.F;
            double d = rng.Range(0.9f, 1.2f);
            uint rail = BuildingGrammar.Railing[rng.Int(0, BuildingGrammar.Railing.Length - 1)];
            MeshKit.Box(m, f, uc - 0.5 * bw, uc + 0.5 * bw, floorBase - 0.15, floorBase, 0, d, BuildingGrammar.Concrete, BoxFaces.All & ~BoxFaces.Back);
            MeshKit.Box(m, f, uc - 0.5 * bw, uc + 0.5 * bw, floorBase, floorBase + 0.95, d - 0.05, d, rail, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            MeshKit.Panel(m, f, uc - 0.45, floorBase, uc + 0.45, floorBase + 2.1, 0.01, BuildingGrammar.Glass);
        }

        /// <summary>Generic signboards over a shopfront: one board, or 3-8 stacked boards in Thamel.</summary>
        private static void Sign(ref House h, ref GrammarRng rng, double u0, double u1, double v, MeshData m)
        {
            if (u1 - u0 < 0.8) return;
            KitFrame f = h.F;
            int n = h.Style.SignsMax > 1 ? rng.Int(Math.Min(2, h.Style.SignsMin), Math.Min(4, h.Style.SignsMax)) : 1;
            for (int k = 0; k < n; k++)
            {
                uint col = BuildingGrammar.Sign[rng.Int(0, BuildingGrammar.Sign.Length - 1)];
                double hgt = rng.Range(0.6f, 0.9f), a = u0, b = u1;
                if (n > 1)
                {
                    double seg = (u1 - u0) / Math.Min(n, 2);
                    a = u0 + seg * (k % 2);
                    b = a + seg - 0.1;
                }
                double y = v + 0.05 + (k / 2) * 1.0;
                MeshKit.Box(m, f, a, b, y, y + hgt, 0, 0.08, col, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // RANA_PALACE
        // ---------------------------------------------------------------------------------------------------------

        private static void RanaFront(ref House h, double u0, double u1, MeshData m)
        {
            KitFrame f = h.F;
            double w = u1 - u0;
            int bays = Math.Max(1, (int)Math.Round(w / 3.3));
            for (int k = 0; k < h.Plan.Storeys; k++)
            {
                double b0 = Floor(ref h, k), sH = StoreyH(ref h, k);
                for (int bay = 0; bay < bays; bay++)
                {
                    double uc = u0 + (bay + 0.5) * w / bays, wh = Math.Min(3.0, sH - 0.8);
                    MeshKit.Box(m, f, uc - 0.75, uc + 0.75, b0 + 0.5, b0 + 0.6 + wh, 0, 0.08, MeshColor.FromHex(0xFFFFFF), BoxFaces.Wall);
                    MeshKit.Panel(m, f, uc - 0.6, b0 + 0.6, uc + 0.6, b0 + 0.5 + wh, 0.085, BuildingGrammar.RanaShutter);
                }
                if (h.Drop < 3) MeshKit.Box(m, f, u0, u1, b0 - 0.15, b0 + 0.1, 0, 0.2, MeshColor.FromHex(0xFFFFFF), BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            }
            double top = h.Top - h.Ground;
            MeshKit.Box(m, f, u0 - 0.3, u1 + 0.3, top - 0.35, top, 0, 0.5, MeshColor.FromHex(0xFFFFFF), BoxFaces.All & ~BoxFaces.Back);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Flat roofs and roof props
        // ---------------------------------------------------------------------------------------------------------

        private static void FlatRoof(ref House h, Scratch s, int n, double parapet, MeshData m)
        {
            double y = h.Top;
            // Inner parapet faces and the deck.
            if (parapet > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = i + 1 == n ? 0 : i + 1;
                    double dx = s.X[j] - s.X[i], dz = s.Z[j] - s.Z[i];
                    MeshKit.Quad(m, s.X[i], y, s.Z[i], s.X[j], y, s.Z[j], s.X[j], y + parapet, s.Z[j], s.X[i], y + parapet, s.Z[i], -dz, 0, dx,
                                 MeshColor.Scale(h.Plan.Wall, 0.9f));
                }
            }
            bool convex = Polygon.IsConvex(s.X, s.Z, n, 0.0);
            int tris = Polygon.Triangulate(s.X, s.Z, n, s.Tris, s.Next, s.Prev, convex);
            for (int t = 0; t < tris; t++)
            {
                int a = s.Tris[3 * t], b = s.Tris[3 * t + 1], c = s.Tris[3 * t + 2];
                MeshKit.Tri(m, s.X[a], y, s.Z[a], s.X[b], y, s.Z[b], s.X[c], y, s.Z[c], 0, 1, 0, h.Plan.RoofColour);
            }
            if (h.Colliders != null) h.Colliders.AddBox(h.CX, h.CZ, h.Base, y, 0.5 * h.L, 0.5 * h.D, h.F.UX, h.F.UZ, GenColliderFlags.Walkable, GenColliders.Concrete);
            Props(ref h, s, n, y, m);
        }

        private static bool Inside(Scratch s, int n, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((s.Z[i] > z) != (s.Z[j] > z) && x < (s.X[j] - s.X[i]) * (z - s.Z[i]) / (s.Z[j] - s.Z[i]) + s.X[i]) inside = !inside;
            return inside;
        }

        /// <summary>A point inside the footprint at frame coordinates (u, w), pulled toward the centroid until inside.</summary>
        private static bool Spot(ref House h, Scratch s, int n, double u, double w, out double x, out double z)
        {
            double y;
            h.F.ToWorld(u, 0, w, out x, out y, out z);
            for (int k = 0; k < 4; k++)
            {
                if (Inside(s, n, x, z)) return true;
                x = 0.5 * (x + h.CX);
                z = 0.5 * (z + h.CZ);
            }
            return Inside(s, n, x, z);
        }

        /// <summary>Roof props of flat roofs (W2_DESIGN 2.6), seeded per building; all dropped at drop level 4.</summary>
        private static void Props(ref House h, Scratch s, int n, double y, MeshData m)
        {
            var rng = new GrammarRng(h.Plan.Seed, PurposeProps);
            bool house = h.Plan.Archetype == BuildingArchetype.ModernUrban || h.Plan.Archetype == BuildingArchetype.NewarHybrid ||
                         h.Plan.Archetype == BuildingArchetype.Generic;
            if (!house) return;
            // Stair cabin (mumty) on every flat roof of 3+ storeys: 8-15% of the roof area, 2.4 m high, at the back.
            if (h.Plan.Storeys >= 3)
            {
                double side = Math.Max(1.8, Math.Min(3.5, Math.Sqrt(rng.Range(0.08f, 0.15f) * h.AreaM2)));
                double x, z;
                if (Spot(ref h, s, n, 0.5 * h.L, -0.7 * h.D, out x, out z))
                {
                    MeshKit.OrientedBox(m, x, z, y, y + 2.4, 0.5 * side, 0.5 * side, h.F.UX, h.F.UZ, h.Plan.Front, BoxFaces.All & ~BoxFaces.Bottom);
                    var cab = new KitFrame(x, y, z, h.F.UX, h.F.UZ);
                    MeshKit.Panel(m, cab, -0.4, 0, 0.4, 2.0, 0.5 * side + 0.01, MeshColor.FromHex(0x5A3A28));
                }
            }
            if (h.Drop >= 4) return;
            // Water tanks on 60-80% of flat roofs, 1-3 each, on a stand.
            if (rng.Chance(0.7f))
            {
                int tanks = rng.Int(1, 3);
                for (int k = 0; k < tanks; k++)
                {
                    double x, z;
                    if (!Spot(ref h, s, n, h.L * rng.Range(0.2f, 0.8f), -h.D * rng.Range(0.2f, 0.5f), out x, out z)) continue;
                    double r = 0.5 * rng.Range(0.9f, 1.3f), th = rng.Range(1.0f, 1.6f), stand = rng.Range(0.3f, 1.5f);
                    MeshKit.OrientedBox(m, x, z, y, y + stand, r, r, h.F.UX, h.F.UZ, BuildingGrammar.Concrete, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                    MeshKit.Cylinder(m, x, z, r, y + stand, y + stand + th, 8, true, BuildingGrammar.Tank[rng.Int(0, BuildingGrammar.Tank.Length - 1)]);
                }
            }
            // Solar water heater on 15-25%: a 2 × 1.5 m rack tilted 30-45 degrees to the south.
            if (rng.Chance(0.2f))
            {
                double x, z;
                if (Spot(ref h, s, n, h.L * 0.3, -h.D * 0.3, out x, out z))
                {
                    double tilt = rng.Range(30f, 45f) * Math.PI / 180;
                    var sol = new KitFrame(x, y + 0.4, z, 1, 0); // U east, W south... W = (U.z, -U.x) = (0, -1): south
                    double rise = 1.5 * Math.Sin(tilt), run = 1.5 * Math.Cos(tilt);
                    MeshKit.QuadLocal(m, sol, -1.0, 0, 0.5 * run, 1.0, 0, 0.5 * run, 1.0, rise, -0.5 * run, -1.0, rise, -0.5 * run, 0, 1, 1, MeshColor.FromHex(0x2E4A7A));
                    MeshKit.QuadLocal(m, sol, -1.0, 0, 0.5 * run, 1.0, 0, 0.5 * run, 1.0, rise, -0.5 * run, -1.0, rise, -0.5 * run, 0, -1, -1, MeshColor.FromHex(0x9AA0A6));
                }
            }
            // Rebar stubs on 30-50% of growing MODERN houses of 4+ storeys.
            if (h.Plan.Archetype == BuildingArchetype.ModernUrban && h.Plan.Storeys >= 4 && rng.Chance(0.4f))
            {
                int stubs = rng.Int(4, 8);
                for (int k = 0; k < stubs; k++)
                {
                    double x, z;
                    double u = k % 2 == 0 ? 0.3 : h.L - 0.3, w = -h.D * (k / 2) / Math.Max(1, stubs / 2 - 1) * 0.9 - 0.3;
                    if (!Spot(ref h, s, n, u, w, out x, out z)) continue;
                    MeshKit.OrientedBox(m, x, z, y, y + rng.Range(0.3f, 1.0f), 0.05, 0.05, h.F.UX, h.F.UZ, BuildingGrammar.Rust, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                }
            }
            // Satellite dish on 10-20%.
            if (rng.Chance(0.15f))
            {
                double x, z;
                if (Spot(ref h, s, n, h.L * 0.8, -0.3, out x, out z))
                    MeshKit.Frustum(m, x, z, 0.4, y + 0.6, 0.05, y + 0.85, 6, false, MeshColor.FromHex(0xE6E6E6));
            }
            // Rooftop restaurant umbrellas (Thamel, Boudha kora).
            if (h.Style.RoofTerraceShare > 0 && rng.Chance(h.Style.RoofTerraceShare))
            {
                int umbrellas = rng.Int(1, 3);
                for (int k = 0; k < umbrellas; k++)
                {
                    double x, z;
                    if (!Spot(ref h, s, n, h.L * (k + 1) / (umbrellas + 1), -h.D * 0.35, out x, out z)) continue;
                    uint col = BuildingGrammar.Sign[rng.Int(0, 3)];
                    MeshKit.Cylinder(m, x, z, 0.03, y, y + 2.2, 4, false, MeshColor.FromHex(0xDDDDDD));
                    MeshKit.Frustum(m, x, z, 1.2, y + 1.95, 0.0, y + 2.4, 8, false, col);
                    MeshKit.Frustum(m, x, z, 1.2, y + 1.95, 1.0, y + 1.93, 8, false, col);
                }
            }
        }
    }
}
