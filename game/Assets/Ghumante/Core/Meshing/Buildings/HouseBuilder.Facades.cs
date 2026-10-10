using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;

namespace Ghumante.Core.Meshing
{
    internal static partial class HouseBuilder
    {
        /// <summary>Bay count: <c>clamp(round(width / 1.6), 1, 7)</c>, odd from 4.5 m (a centre bay).</summary>
        private static int Bays(double width)
        {
            int n = (int)Math.Round(width / 1.6);
            n = n < 1 ? 1 : n > 7 ? 7 : n;
            if (width >= 4.5 && n % 2 == 0) n = n + 1 > 7 ? n - 1 : n + 1;
            return n;
        }

        /// <summary>The street facade of a plot, by archetype.</summary>
        private static void Facade(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            if (p.FU1 - p.FU0 < 0.8)
            {
                // A sliver of front: a plain wall.
                Wall(ref h, ref p, h.F, p.FU0, p.FU1, h.Base - h.Ground, p.Top + p.Parapet, s.Holes, 0, p.Front, p.FrontCh, m);
                return;
            }
            switch (p.Arch)
            {
                case BuildingArchetype.Newar:
                    NewarFront(ref h, s, ref p, ref rng, m, p.Storeys);
                    break;
                case BuildingArchetype.NewarHybrid:
                    NewarFront(ref h, s, ref p, ref rng, m, Math.Min(3, p.Storeys));
                    HybridUpper(ref h, s, ref p, ref rng, m);
                    break;
                case BuildingArchetype.RanaPalace:
                    RanaFront(ref h, s, ref p, ref rng, m);
                    break;
                default:
                    ModernFront(ref h, s, ref p, ref rng, m);
                    break;
            }
        }

        /// <summary>A wall band [v0, v1] over [u0, u1] of frame f with holes, painted as a wall; a band that reaches an end
        /// of the facade's layout range runs on to the front wall's own end (<see cref="Plot.GU0"/>, <see cref="Plot.GU1"/>:
        /// the square corner of a level that does not round it).</summary>
        private static void Wall(ref House h, ref Plot p, in KitFrame f, double u0, double u1, double v0, double v1, KitHole[] holes, int nh, uint c,
                                 MaterialChannel ch, MeshData m)
        {
            if (u0 <= p.FU0 + 1e-6) u0 = Math.Min(u0, p.GU0);
            if (u1 >= p.FU1 - 1e-6) u1 = Math.Max(u1, p.GU1);
            int v = m.VertexCount;
            FacadeKit.WallWithHoles(m, f, u0, u1, v0, v1, 0, holes, h.Det.Flat ? 0 : nh, c); // flat: openings are laid on the wall
            WallPaint(ref h, ch, 1f).Apply(m, v);
        }

        /// <summary>
        /// The flat level's opening (<see cref="Detail.Flat"/>): no hole and no reveal; the fill (a dark room, glass, a
        /// leaf, a shutter) a few millimetres proud of the wall, shaded as if set back, under a lintel board running out
        /// in ears (its shadow face too) and, on windows, a sill board: 6-10 triangles. A negative
        /// <paramref name="side"/> draws the fill alone.
        /// </summary>
        private static void FlatOpening(ref House h, in KitFrame f, in KitHole o, uint fill, MaterialChannel fillCh, double side, double ears, uint frame,
                                        MaterialChannel frameCh, bool sill, MeshData m)
        {
            int v0 = m.VertexCount;
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, 0.006, fill);
            Fixed(ref h, f, m, v0, fillCh, 0.8f);
            if (side < 0) return;
            v0 = m.VertexCount;
            double proud = Proud(ref h, f, o.U0 - side - ears, o.U1 + side + ears, sill ? o.V0 - 0.09 : o.V1, 0.06);
            MeshKit.Box(m, f, o.U0 - side - ears, o.U1 + side + ears, o.V1, o.V1 + 0.11, 0, proud, frame, BoxFaces.Front | BoxFaces.Bottom);
            if (sill) MeshKit.Box(m, f, o.U0 - side - 0.5 * ears, o.U1 + side + 0.5 * ears, o.V0 - 0.09, o.V0, 0, proud, frame, BoxFaces.Front | BoxFaces.Top);
            Fixed(ref h, f, m, v0, frameCh);
        }

        // =============================================================================================================
        // NEWAR (G..floors-1) — Bhaktapur, Patan, Kirtipur, Asan rows
        // =============================================================================================================

        private static void NewarFront(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m, int floors)
        {
            KitFrame f = h.F;
            double u0 = p.FU0, u1 = p.FU1, w = u1 - u0;
            int bays = Bays(w);
            double bayW = w / bays, uc = u0 + 0.5 * w;
            uint wood = p.Wood, carved = MeshColor.Scale(p.Wood, 1.12f);
            uint lattice = MeshColor.Lerp(p.Wood, BuildingGrammar.SalMid, 0.5f);
            bool hybrid = p.Arch == BuildingArchetype.NewarHybrid;
            int last = Math.Min(floors, p.Storeys);
            KitHole[] holes = s.Holes;

            // ---- G: shop dalan or house door on the pikha apron.
            double g0 = FloorBase(s, p, 0), g1 = last > 1 || p.Storeys > 1 ? FloorBase(s, p, 1) : p.Top + p.Parapet;
            double gH = FloorBase(s, p, 1) - g0;
            int nh = 0;
            bool shop = p.Shop;
            if (shop)
            {
                // One or two dalan openings between timber posts.
                int openings = w > 5.5 ? 2 : 1;
                double sh = Math.Min(gH - 0.25, 2.1);
                for (int k = 0; k < openings; k++)
                {
                    double a = u0 + 0.35 + (w - 0.7) * k / openings, b = u0 + 0.35 + (w - 0.7) * (k + 1) / openings - (k + 1 < openings ? 0.18 : 0);
                    holes[nh++] = new KitHole(a, g0, b, g0 + sh);
                }
            }
            else
            {
                double dw = rng.Range(0.75f, 0.9f), dh = rng.Range(1.45f, 1.7f);
                holes[nh++] = new KitHole(uc - 0.5 * dw, g0, uc + 0.5 * dw, g0 + dh);
                if (w > 3.2)
                    for (int k = 0; k < bays; k++)
                    {
                        double bc = u0 + (k + 0.5) * bayW;
                        if (Math.Abs(bc - uc) < 0.9) continue;
                        holes[nh++] = new KitHole(bc - 0.24, g0 + 0.95, bc + 0.24, g0 + 1.55);
                    }
            }
            Wall(ref h, ref p, f, u0, u1, h.Base - h.Ground, g1, holes, nh, p.Front, p.FrontCh, m);
            if (shop) NewarShop(ref h, ref p, rng.Fork(), holes, nh, g0, m);
            else
            {
                NewarDoor(ref h, ref p, rng.Fork(), holes[0], m);
                for (int k = 1; k < nh; k++) SmallWindow(ref h, ref p, holes[k], m);
                Pikha(ref h, ref p, u0, u1, m);
            }
            // The tiled pent roof over the ground floor (Patan, Bhaktapur, the Kathmandu core).
            float pent = h.Plan.Profile == StyleProfile.Patan ? 0.45f : h.Plan.Profile == StyleProfile.Bhaktapur || h.Plan.Profile == StyleProfile.KathmanduCore ||
                         h.Plan.Profile == StyleProfile.Kirtipur ? 0.3f : 0.15f;
            if (p.Storeys >= 2 && rng.Chance(pent)) Hood(ref h, ref p, rng.Fork(), f, u0, u1, g1 + 0.06, rng.Range(0.6f, 0.85f), true, m);

            // ---- Upper floors.
            for (int k = 1; k < last; k++)
            {
                double b0 = FloorBase(s, p, k), b1 = FloorBase(s, p, k + 1), sH = b1 - b0;
                bool top = k == last - 1 && !hybrid;
                double vTop = top ? p.Top + p.Parapet : b1;
                bool attic = top && k >= 3 && p.Arch == BuildingArchetype.Newar;
                bool sanjhya = k == 2 || k > 2 && !attic && p.Arch == BuildingArchetype.Newar && rng.Chance(0.25f);
                nh = 0;
                double sw = 0, proj = 0, sv = b0 + 0.28, ssh = Math.Min(1.35, sH - 0.55);
                bool flush = false;
                if (sanjhya)
                {
                    sw = w >= 7 && rng.Chance(0.15f) ? w - 0.6 : w >= 4.5 ? Math.Min(rng.Range(2.4f, 3.6f), w - 0.6) : Math.Min(1.3, w - 0.4);
                    proj = rng.Range(0.32f, 0.55f);
                    for (int bay = 0; bay < bays; bay++)
                    {
                        double bc = u0 + (bay + 0.5) * bayW;
                        if (Math.Abs(bc - uc) < 0.5 * sw + 0.45) continue;
                        Tiki(holes, ref nh, bc, b0, bayW, sH);
                    }
                    // Where a road corridor leaves no room for the bay below 4.5 m it is set into the wall instead.
                    flush = sw >= 0.6 && ssh >= 0.5 && SanjhyaDepth(ref h, f, uc - 0.5 * sw, uc + 0.5 * sw, sv, proj) <= 0;
                }
                else if (!attic)
                {
                    for (int bay = 0; bay < bays; bay++) Tiki(holes, ref nh, u0 + (bay + 0.5) * bayW, b0, bayW, sH);
                }
                int windows = nh;
                if (flush && nh < holes.Length) holes[nh++] = new KitHole(uc - 0.5 * sw, sv, uc + 0.5 * sw, sv + ssh);
                Wall(ref h, ref p, f, u0, u1, b0, vTop, holes, nh, p.Front, p.FrontCh, m);
                for (int i = 0; i < windows; i++) Tikijhya(ref h, ref p, holes[i], wood, carved, lattice, m);
                if (sanjhya) Sanjhya(ref h, ref p, rng.Fork(), uc, sw, sv, ssh, proj, flush && nh > windows, wood, carved, lattice, m);
                if (attic) Gajhya(ref h, ref p, uc, Math.Min(rng.Range(0.9f, 1.4f), w - 0.5), b0 + 0.25, Math.Min(0.75, sH - 0.45), wood, carved, lattice, m);
                if (h.Det.Bands) FloorBand(ref h, ref p, u0, u1, b0, m);
                GrammarRng plants = rng.Fork();
                if (k == 1 && h.Det.Small && plants.Chance(0.12f)) SillPlant(ref h, ref p, plants, holes, nh, m);
            }
        }

        private static void Tiki(KitHole[] holes, ref int nh, double bc, double b0, double bayW, double sH)
        {
            double ww = Math.Min(bayW - 0.42, 0.72), wh = Math.Min(0.95, sH - 0.75);
            if (ww < 0.3 || wh < 0.35 || nh >= holes.Length) return;
            holes[nh++] = new KitHole(bc - 0.5 * ww, b0 + 0.42, bc + 0.5 * ww, b0 + 0.42 + wh);
        }

        /// <summary>The floor band at a floor line: a two-course brick corbel, or a carved timber band with joist ends
        /// (Bhaktapur).</summary>
        private static void FloorBand(ref House h, ref Plot p, double u0, double u1, double v, MeshData m)
        {
            KitFrame f = h.F;
            int v0 = m.VertexCount;
            if (h.Style.CarvedBands && p.Arch == BuildingArchetype.Newar)
            {
                FacadeKit.Ledge(m, f, u0, u1, v - 0.13, 0.22, Proud(ref h, f, u0, u1, v - 0.13, 0.09), 0.04, 3, MeshColor.Scale(p.Wood, 1.15f));
                Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
                double joist = Allow(ref h, f, u0, u1, v - 0.25, 0.14);
                if (h.Det.Courses && joist > 0.1)
                {
                    v0 = m.VertexCount;
                    for (double u = u0 + 0.25; u < u1 - 0.15; u += 0.55)
                        MeshKit.Box(m, f, u, u + 0.1, v - 0.25, v - 0.13, 0, joist, MeshColor.Scale(p.Wood, 0.9f), BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left);
                    Fixed(ref h, f, m, v0, MaterialChannel.Wood);
                }
                return;
            }
            uint band = h.Style.HasBrick ? MeshColor.Lerp(p.Front, h.Style.BrickJoint, 0.35f) : MeshColor.Scale(p.Front, 0.8f);
            FacadeKit.Corbel(m, f, u0, u1, v - 0.16, 2, 0.11, 0.5 * Proud(ref h, f, u0, u1, v - 0.16, 0.12, 0.03), 3, band);
            Fixed(ref h, f, m, v0, p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.BrickGlazed : MaterialChannel.Brick);
        }

        /// <summary>Pikha: the stone-capped front apron (0.3-0.45 high, up to 0.6 deep, clipped out of the road).</summary>
        private static void Pikha(ref House h, ref Plot p, double u0, double u1, MeshData m)
        {
            KitFrame f = h.F;
            double ph = Math.Max(0.3, p.Plinth + 0.12);
            double d = Allow(ref h, f, u0, u1, 0, 0.6);
            if (d < 0.2) return;
            int v0 = m.VertexCount;
            int seg = h.Det.Lite ? 0 : 1;
            KitRound.BoxU(m, f, u0, u1, h.Base - h.Ground, ph - 0.07, 0, d - 0.03, 0.02, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, MeshColor.Scale(p.Wall, 0.85f));
            Free(ref h, m, v0, MaterialChannel.Brick);
            v0 = m.VertexCount;
            KitRound.BoxU(m, f, u0 - 0.02, u1 + 0.02, ph - 0.07, ph, 0, d, 0.03, seg, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right, BuildingGrammar.PlinthStone);
            Free(ref h, m, v0, MaterialChannel.Stone);
            if (h.Colliders != null)
            {
                double cx, cy, cz;
                f.ToWorld(0.5 * (u0 + u1), 0, 0.5 * d, out cx, out cy, out cz);
                h.Colliders.AddBox(cx, cz, h.Base, h.Ground + ph, 0.5 * (u1 - u0), 0.5 * d, f.UX, f.UZ, GenColliderFlags.Walkable, GenColliders.Stone);
            }
        }

        /// <summary>The lite level's frame around an opening: a lintel running out in ears, a sill and two jambs as plain
        /// boards a few centimetres proud of the wall (12 triangles; 8 without the sill).</summary>
        private static void LiteFrame(ref House h, in KitFrame f, in KitHole o, double side, double ears, double proud, uint c, MaterialChannel ch,
                                      MeshData m, bool sill = true)
        {
            int v0 = m.VertexCount;
            proud = Proud(ref h, f, o.U0 - side - ears, o.U1 + side + ears, o.V0 - 0.09, proud);
            MeshKit.Box(m, f, o.U0 - side - ears, o.U1 + side + ears, o.V1, o.V1 + 0.11, 0, proud, c, BoxFaces.Front | BoxFaces.Bottom);
            if (sill) MeshKit.Box(m, f, o.U0 - side - 0.5 * ears, o.U1 + side + 0.5 * ears, o.V0 - 0.09, o.V0, 0, proud, c, BoxFaces.Front | BoxFaces.Top);
            MeshKit.Panel(m, f, o.U0 - side, o.V0, o.U0, o.V1, 0.6 * proud, c);
            MeshKit.Panel(m, f, o.U1, o.V0, o.U1 + side, o.V1, 0.6 * proud, c);
            Fixed(ref h, f, m, v0, ch);
        }

        /// <summary>The low carved house door: recessed leaves, a carved frame with lintel and sill ears, a threshold
        /// stone, a painted panel above and vermilion on the lintel.</summary>
        private static void NewarDoor(ref House h, ref Plot p, GrammarRng rng, in KitHole d, MeshData m)
        {
            KitFrame f = h.F;
            const double Reveal = 0.32;
            int v0 = m.VertexCount;
            if (h.Det.Flat)
            {
                FlatOpening(ref h, f, d, MeshColor.Scale(p.Wood, 1.05f), MaterialChannel.Wood, 0.13, 0.3, MeshColor.Scale(p.Wood, 1.12f), MaterialChannel.WoodCarved, false, m);
                return;
            }
            if (h.Det.Lite)
            {
                FacadeKit.Reveal(m, f, d, 0, Reveal, MeshColor.Scale(p.Front, 0.9f), false);
                Recess(ref h, f, m, v0, p.FrontCh, Reveal);
                v0 = m.VertexCount;
                MeshKit.Panel(m, f, d.U0, d.V0, d.U1, d.V1, -Reveal + 0.12, MeshColor.Scale(p.Wood, 1.05f));
                Recess(ref h, f, m, v0, MaterialChannel.Wood, Reveal, 0.9f);
                LiteFrame(ref h, f, d, 0.13, 0.3, 0.08, MeshColor.Scale(p.Wood, 1.12f), MaterialChannel.WoodCarved, m, false);
                return;
            }
            FacadeKit.Reveal(m, f, d, 0, Reveal, MeshColor.Scale(p.Front, 0.9f), false);
            Recess(ref h, f, m, v0, p.FrontCh, Reveal);
            v0 = m.VertexCount;
            double mid = 0.5 * (d.U0 + d.U1);
            uint leaf = MeshColor.Scale(p.Wood, 1.05f);
            KitRound.BoxV(m, f, d.U0 + 0.02, mid - 0.01, d.V0, d.V1 - 0.02, -Reveal + 0.1, -Reveal + 0.16, 0.015, 1, BoxFaces.Front, leaf);
            KitRound.BoxV(m, f, mid + 0.01, d.U1 - 0.02, d.V0, d.V1 - 0.02, -Reveal + 0.1, -Reveal + 0.16, 0.015, 1, BoxFaces.Front, leaf);
            if (h.Det.Small)
            {
                // Raised panels on the leaves and a brass knocker ring.
                for (int side = 0; side < 2; side++)
                {
                    double a = side == 0 ? d.U0 + 0.08 : mid + 0.06, b = side == 0 ? mid - 0.06 : d.U1 - 0.08;
                    MeshKit.Box(m, f, a, b, d.V0 + 0.15, d.V0 + 0.6, -Reveal + 0.16, -Reveal + 0.19, MeshColor.Scale(leaf, 1.12f), BoxFaces.Wall | BoxFaces.Bottom);
                    MeshKit.Box(m, f, a, b, d.V0 + 0.75, d.V1 - 0.15, -Reveal + 0.16, -Reveal + 0.19, MeshColor.Scale(leaf, 1.12f), BoxFaces.Wall | BoxFaces.Bottom);
                }
            }
            Recess(ref h, f, m, v0, MaterialChannel.Wood, Reveal, 0.9f);
            // Carved frame, lintel and sill with ears.
            v0 = m.VertexCount;
            uint frame = MeshColor.Scale(p.Wood, 1.12f);
            int seg = h.Det.Segs > 0 ? 1 : 0;
            // Frame depth: what the road in front leaves (the parts are scaled to it, never pressed into the wall).
            double fd = Proud(ref h, f, d.U0 - 0.45, d.U1 + 0.45, d.V0 - 0.12, 0.1, 0.02), k = fd / 0.1;
            KitRound.BoxV(m, f, d.U0 - 0.13, d.U0, d.V0, d.V1, 0, 0.07 * k, 0.02 * k, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, frame);
            KitRound.BoxV(m, f, d.U1, d.U1 + 0.13, d.V0, d.V1, 0, 0.07 * k, 0.02 * k, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, frame);
            KitRound.BoxU(m, f, d.U0 - 0.45, d.U1 + 0.45, d.V1, d.V1 + 0.14, 0, fd, 0.025 * k, seg, BoxFaces.All & ~BoxFaces.Back, frame);
            KitRound.BoxU(m, f, d.U0 - 0.3, d.U1 + 0.3, d.V1 + 0.14, d.V1 + 0.22, 0, 0.06 * k, 0.02 * k, seg, BoxFaces.All & ~BoxFaces.Back, MeshColor.Scale(frame, 0.85f));
            Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            // Threshold stone with ears.
            v0 = m.VertexCount;
            KitRound.BoxU(m, f, d.U0 - 0.3, d.U1 + 0.3, d.V0 - 0.12, d.V0, -Reveal, 0.8 * fd, 0.02 * k, seg, BoxFaces.All & ~BoxFaces.Back, BuildingGrammar.PlinthStone);
            Fixed(ref h, f, m, v0, MaterialChannel.Stone);
            // Painted deity panel (a small torana board) with vermilion and a marigold garland on festive houses.
            if (h.Det.Small)
            {
                v0 = m.VertexCount;
                double pv = d.V1 + 0.26;
                double pd = 0.4 * fd;
                KitRound.BoxU(m, f, mid - 0.32, mid + 0.32, pv, pv + 0.36, 0, pd, 0.3 * pd, 1, BoxFaces.All & ~BoxFaces.Back, BuildingGrammar.Ochre);
                MeshKit.Box(m, f, mid - 0.11, mid + 0.11, pv + 0.06, pv + 0.3, pd, pd + 0.015, BuildingGrammar.Sindoor, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                // The garland droops 0.25 m from 0.12 m in front of the lintel: only where it clears the road.
                if (rng.Chance(0.25f) && Fits(ref h, f, d.U0 - 0.1, d.U1 + 0.1, d.V1 - 0.3, 0.18))
                {
                    v0 = m.VertexCount;
                    double gx0, gy0, gz0, gx1, gy1, gz1;
                    f.ToWorld(d.U0 - 0.05, d.V1 + 0.05, 0.12, out gx0, out gy0, out gz0);
                    f.ToWorld(d.U1 + 0.05, d.V1 + 0.05, 0.12, out gx1, out gy1, out gz1);
                    double mx = 0.5 * (gx0 + gx1), my = gy0 - 0.25, mz = 0.5 * (gz0 + gz1);
                    KitRound.Tube(m, Pts(gx0, gy0, gz0, mx, my, mz, gx1, gy1, gz1), _py3, _pz3, null, 0.045, 3, 5, false, BuildingGrammar.Marigold);
                    Fixed(ref h, f, m, v0, MaterialChannel.Foliage);
                }
            }
        }

        [ThreadStatic] private static double[] _px3, _py3, _pz3;

        private static double[] Pts(double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            if (_px3 == null)
            {
                _px3 = new double[3];
                _py3 = new double[3];
                _pz3 = new double[3];
            }
            _px3[0] = x0;
            _py3[0] = y0;
            _pz3[0] = z0;
            _px3[1] = x1;
            _py3[1] = y1;
            _pz3[1] = z1;
            _px3[2] = x2;
            _py3[2] = y2;
            _pz3[2] = z2;
            return _px3;
        }

        /// <summary>A small ground-floor window: frame, lintel and sill with ears, iron bars, dark inside (about 50
        /// triangles).</summary>
        private static void SmallWindow(ref House h, ref Plot p, in KitHole o, MeshData m)
        {
            KitFrame f = h.F;
            if (h.Det.Flat)
            {
                FlatOpening(ref h, f, o, BuildingGrammar.Interior, MaterialChannel.Plain, 0.08, 0.15, p.Wood, MaterialChannel.WoodCarved, true, m);
                return;
            }
            int v0 = m.VertexCount;
            FacadeKit.Reveal(m, f, o, 0, 0.3, MeshColor.Scale(p.Front, 0.9f), !h.Det.Lite);
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -0.3, BuildingGrammar.Interior);
            Recess(ref h, f, m, v0, MaterialChannel.Plain, 0.3);
            if (h.Det.Lite)
            {
                LiteFrame(ref h, f, o, 0.08, 0.15, 0.06, p.Wood, MaterialChannel.WoodCarved, m);
                return;
            }
            v0 = m.VertexCount;
            double fd = Proud(ref h, f, o.U0 - 0.25, o.U1 + 0.25, o.V0 - 0.08, 0.07);
            MeshKit.Box(m, f, o.U0 - 0.09, o.U0, o.V0, o.V1, 0, 0.7 * fd, p.Wood, BoxFaces.Front | BoxFaces.Left);
            MeshKit.Box(m, f, o.U1, o.U1 + 0.09, o.V0, o.V1, 0, 0.7 * fd, p.Wood, BoxFaces.Front | BoxFaces.Right);
            FacadeKit.Ledge(m, f, o.U0 - 0.25, o.U1 + 0.25, o.V1, 0.09, fd, 0.025, 3, p.Wood);
            FacadeKit.Ledge(m, f, o.U0 - 0.2, o.U1 + 0.2, o.V0 - 0.08, 0.08, fd, 0.025, 3, p.Wood);
            Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            if (h.Det.Grilles)
            {
                v0 = m.VertexCount;
                for (int k = 1; k <= 3; k++)
                {
                    double u = o.U0 + (o.U1 - o.U0) * k / 4;
                    MeshKit.Box(m, f, u - 0.012, u + 0.012, o.V0, o.V1, -0.12, -0.09, BuildingGrammar.SteelDark, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
            }
        }

        /// <summary>Tikijhya: the diagonal lattice window in a carved frame whose lintel and sill run out in ears,
        /// set into a 0.3 m reveal over a dark room (about 80 triangles).</summary>
        private static void Tikijhya(ref House h, ref Plot p, in KitHole o, uint wood, uint carved, uint lattice, MeshData m)
        {
            KitFrame f = h.F;
            int v0;
            if (h.Det.Flat)
            {
                // The carved frame's lintel and sill with their ears over a dark window and a coarse lattice on it.
                FlatOpening(ref h, f, o, BuildingGrammar.Interior, MaterialChannel.Plain, 0.11, 0.24, carved, MaterialChannel.WoodCarved, true, m);
                v0 = m.VertexCount;
                FacadeKit.Lattice(m, f, o.U0, o.V0, o.U1, o.V1, 0.012, h.Det.LatticePitch, 0.05, 0, (p.Seed >> 11) % 10 < 3, lattice, (p.Seed & 7) / 8.0);
                Fixed(ref h, f, m, v0, MaterialChannel.Wood, 0.9f);
                return;
            }
            v0 = m.VertexCount;
            FacadeKit.Reveal(m, f, o, 0, 0.3, MeshColor.Scale(p.Front, 0.88f), !h.Det.Lite);
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -0.3, BuildingGrammar.Interior);
            Recess(ref h, f, m, v0, p.FrontCh, 0.3);
            if (h.Det.Lite)
            {
                // A plain carved-frame board with long ears and a coarse lattice: the window still reads at 30 m.
                LiteFrame(ref h, f, o, 0.11, 0.24, 0.07, carved, MaterialChannel.WoodCarved, m);
                v0 = m.VertexCount;
                FacadeKit.Lattice(m, f, o.U0, o.V0, o.U1, o.V1, -0.05, h.Det.LatticePitch, 0.045, 0, (p.Seed >> 11) % 10 < 3, lattice, (p.Seed & 7) / 8.0);
                Recess(ref h, f, m, v0, MaterialChannel.Wood, 0.12, 0.95f);
                return;
            }
            v0 = m.VertexCount;
            double fd = Proud(ref h, f, o.U0 - 0.34, o.U1 + 0.34, o.V0 - 0.15, 0.1, 0.02);
            MeshKit.Box(m, f, o.U0 - 0.13, o.U0 + 0.02, o.V0 - 0.04, o.V1 + 0.04, 0, 0.7 * fd, carved, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            MeshKit.Box(m, f, o.U1 - 0.02, o.U1 + 0.13, o.V0 - 0.04, o.V1 + 0.04, 0, 0.7 * fd, carved, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            FacadeKit.Ledge(m, f, o.U0 - 0.34, o.U1 + 0.34, o.V1 + 0.04, 0.13, fd, 0.035, 3, carved);
            FacadeKit.Ledge(m, f, o.U0 - 0.3, o.U1 + 0.3, o.V0 - 0.15, 0.11, 0.9 * fd, 0.03, 3, carved);
            Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            v0 = m.VertexCount;
            FacadeKit.Lattice(m, f, o.U0, o.V0, o.U1, o.V1, -0.05, h.Det.LatticePitch, 0.034, 0, (p.Seed >> 11) % 10 < 3, lattice, (p.Seed & 7) / 8.0);
            Recess(ref h, f, m, v0, MaterialChannel.Wood, 0.12, 0.95f);
        }

        /// <summary>How far a sanjhya over [a, b] with its sill at <paramref name="v0"/> may project its bay (the sill
        /// moulding runs 0.1 m further): <paramref name="proj"/>, less where a road corridor is near below 4.5 m, or 0
        /// when less than 0.12 m is free (the bay is then set flush into the wall).</summary>
        private static double SanjhyaDepth(ref House h, in KitFrame f, double a, double b, double v0, double proj)
        {
            double d = Allow(ref h, f, a - 0.18, b + 0.18, v0 - 0.22, proj + 0.1) - 0.1;
            return d >= 0.12 ? Math.Min(proj, d) : 0;
        }

        /// <summary>
        /// Sanjhya: the projecting bay window of the second floor: a moulded sill on carved brackets, 3-5 units
        /// between carved posts with lattice screens over a solid carved apron, lattice cheeks, a cornice and a small
        /// tiled hood. Never dropped. Every part keeps out of the road below 4.5 m on its own: the bay projects only as
        /// far as the corridor allows, the brackets only where they fit, the cornice and hood by their own height;
        /// with no room at all (<paramref name="flush"/>) the screens sit in a reveal cut into the wall, under a
        /// cornice and hood clipped to what is free.
        /// </summary>
        private static void Sanjhya(ref House h, ref Plot p, GrammarRng rng, double uc, double sw, double v0, double sh, double proj, bool flush,
                                    uint wood, uint carved, uint lattice, MeshData m)
        {
            if (sw < 0.6 || sh < 0.5) return;
            KitFrame f = h.F;
            double a = uc - 0.5 * sw, b = uc + 0.5 * sw;
            double d = flush ? 0 : SanjhyaDepth(ref h, f, a, b, v0, proj);
            if (d <= 0) flush = true;
            bool lite = h.Det.Lite;
            int units = sw >= 3.0 ? 5 : sw >= 1.8 ? 3 : 1;
            double apron = Math.Min(0.32, 0.3 * sh);
            int vs = m.VertexCount;
            if (flush && h.Det.Flat)
            {
                // The bay laid on the wall: the dark screen, posts, aprons and a coarse lattice.
                MeshKit.Panel(m, f, a, v0, b, v0 + sh, 0.006, BuildingGrammar.Interior);
                Fixed(ref h, f, m, vs, MaterialChannel.Plain, 0.8f);
                vs = m.VertexCount;
                for (int i = 0; i <= units; i++)
                {
                    double u = a + sw * i / units;
                    MeshKit.Panel(m, f, Math.Max(a, u - 0.06), v0, Math.Min(b, u + 0.06), v0 + sh, 0.016, carved);
                }
                for (int i = 0; i < units; i++)
                {
                    double ua = a + sw * i / units + 0.06, ub = a + sw * (i + 1) / units - 0.06;
                    MeshKit.Panel(m, f, ua, v0, ub, v0 + apron, 0.01, MeshColor.Scale(wood, 1.1f));
                    FacadeKit.Lattice(m, f, ua, v0 + apron, ub, v0 + sh, 0.012, h.Det.LatticePitch * 1.1, 0.05, 0, false, lattice, 0.37 * i);
                }
                Fixed(ref h, f, m, vs, MaterialChannel.WoodCarved, 0.95f);
            }
            else if (flush)
            {
                // The bay as a deep screen window in the wall: reveal, dark room, posts, apron and lattice inside it.
                const double R = 0.16;
                var o = new KitHole(a, v0, b, v0 + sh);
                FacadeKit.Reveal(m, f, o, 0, R, MeshColor.Scale(p.Front, 0.88f));
                MeshKit.Panel(m, f, a, v0, b, v0 + sh, -R, BuildingGrammar.Interior);
                Recess(ref h, f, m, vs, p.FrontCh, R);
                vs = m.VertexCount;
                for (int i = 0; i <= units; i++)
                {
                    double u = a + sw * i / units;
                    MeshKit.Box(m, f, Math.Max(a, u - 0.06), Math.Min(b, u + 0.06), v0, v0 + sh, -0.07, -0.01, carved, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                Recess(ref h, f, m, vs, MaterialChannel.WoodCarved, R, 0.95f);
                vs = m.VertexCount;
                for (int i = 0; i < units; i++)
                {
                    double ua = a + sw * i / units + 0.06, ub = a + sw * (i + 1) / units - 0.06;
                    MeshKit.Panel(m, f, ua, v0, ub, v0 + apron, -0.05, MeshColor.Scale(wood, 1.1f));
                    FacadeKit.Lattice(m, f, ua, v0 + apron, ub, v0 + sh, -0.04, h.Det.LatticePitch * 1.1, lite ? 0.045 : 0.034, 0, false, lattice, 0.37 * i);
                }
                Recess(ref h, f, m, vs, MaterialChannel.Wood, R, 0.95f);
            }
            else
            {
                // Dark room behind the screens (closes the box).
                MeshKit.Panel(m, f, a, v0, b, v0 + sh, 0.005, BuildingGrammar.Interior);
                Recess(ref h, f, m, vs, MaterialChannel.Plain, 0.1);
                vs = m.VertexCount;
                if (lite)
                {
                    // Sill as a plain board, posts as boards on the screen plane.
                    MeshKit.Box(m, f, a - 0.12, b + 0.12, v0 - 0.12, v0, 0, d + 0.1, carved, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                    for (int i = 0; i <= units; i++)
                    {
                        double u = a + sw * i / units;
                        MeshKit.Panel(m, f, u - 0.06, v0, u + 0.06, v0 + sh, d + 0.02, carved);
                    }
                }
                else
                {
                    // Sill board: an ogee-ish moulding.
                    double[] pw = FacadeKit.ProfileW, pv = FacadeKit.ProfileV;
                    int k = 0;
                    pw[k] = 0;
                    pv[k++] = v0 - 0.22;
                    pw[k] = d * 0.55;
                    pv[k++] = v0 - 0.2;
                    pw[k] = d + 0.06;
                    pv[k++] = v0 - 0.1;
                    pw[k] = d + 0.1;
                    pv[k++] = v0 - 0.04;
                    pw[k] = d + 0.1;
                    pv[k++] = v0;
                    pw[k] = 0;
                    pv[k++] = v0;
                    FacadeKit.SweepU(m, f, a - 0.12, b + 0.12, k, 3, carved, 40);
                    // Brackets under the sill, where they clear the road too.
                    if (Fits(ref h, f, a, b, v0 - 0.5, 0.75 * d))
                    {
                        int brackets = sw > 2.6 ? 4 : 2;
                        for (int i = 0; i < brackets; i++)
                        {
                            double u = a + 0.1 + (sw - 0.32) * i / (brackets - 1);
                            KitRound.BoxW(m, f, u, u + 0.12, v0 - 0.5, v0 - 0.22, 0, d * 0.75, 0.03, 0, BoxFaces.All & ~BoxFaces.Back & ~BoxFaces.Top, carved);
                        }
                    }
                    // Posts dividing the units.
                    for (int i = 0; i <= units; i++)
                    {
                        double u = a + sw * i / units;
                        MeshKit.Box(m, f, u - 0.06, u + 0.06, v0, v0 + sh, d - 0.07, d + 0.02, carved, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    }
                }
                // Cheeks.
                MeshKit.QuadLocal(m, f, a, v0, 0, a, v0, d, a, v0 + sh, d, a, v0 + sh, 0, -1, 0, 0, wood);
                MeshKit.QuadLocal(m, f, b, v0, 0, b, v0, d, b, v0 + sh, d, b, v0 + sh, 0, 1, 0, 0, wood);
                Fixed(ref h, f, m, vs, MaterialChannel.WoodCarved);
                vs = m.VertexCount;
                for (int i = 0; i < units; i++)
                {
                    double ua = a + sw * i / units + 0.06, ub = a + sw * (i + 1) / units - 0.06;
                    MeshKit.Panel(m, f, ua, v0, ub, v0 + apron, d - 0.04, MeshColor.Scale(wood, 1.1f));
                    if (!h.Det.Flat) MeshKit.Panel(m, f, ua, v0 + apron, ub, v0 + sh, d - 0.08, MeshColor.Scale(BuildingGrammar.Interior, 1.2f));
                    FacadeKit.Lattice(m, f, ua, v0 + apron, ub, v0 + sh, d - 0.05, h.Det.LatticePitch * 1.1, lite ? 0.045 : 0.034, 0, false, lattice, 0.37 * i);
                }
                Fixed(ref h, f, m, vs, MaterialChannel.Wood);
            }
            // Cornice, clipped by its own height.
            vs = m.VertexCount;
            double cd = Allow(ref h, f, a - 0.1, b + 0.1, v0 + sh, d + 0.08);
            if (cd >= 0.04)
            {
                if (lite) MeshKit.Box(m, f, a - 0.1, b + 0.1, v0 + sh, v0 + sh + 0.14, 0, cd, carved, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                else FacadeKit.Band(m, f, a - 0.1, b + 0.1, v0 + sh, 0.14, cd, 0.03, 3, carved);
                Fixed(ref h, f, m, vs, MaterialChannel.WoodCarved);
            }
            // The small tiled hood: its front edge is its lowest point.
            double hv = v0 + sh + 0.14, rise = 0.32;
            double hd = Allow(ref h, f, a - 0.18, b + 0.18, hv, d + 0.22);
            uint hoodTile = p.Gable ? p.Roof : BuildingGrammar.JhingatiColour(h.Plan.Profile, ref rng);
            if (hd >= 0.1)
            {
                vs = m.VertexCount;
                MeshKit.QuadLocal(m, f, a - 0.18, hv, hd, b + 0.18, hv, hd, b + 0.18, hv + rise, 0, a - 0.18, hv + rise, 0, 0, 1, 1, hoodTile);
                Fixed(ref h, f, m, vs, MaterialChannel.RoofTile);
                vs = m.VertexCount;
                MeshKit.QuadLocal(m, f, a - 0.18, hv, hd, b + 0.18, hv, hd, b + 0.18, hv + rise, 0, a - 0.18, hv + rise, 0, 0, -1, -0.5, wood);
                MeshKit.TriLocal(m, f, a - 0.18, hv, hd, a - 0.18, hv, 0, a - 0.18, hv + rise, 0, -1, 0, 0, wood);
                MeshKit.TriLocal(m, f, b + 0.18, hv, hd, b + 0.18, hv, 0, b + 0.18, hv + rise, 0, 1, 0, 0, wood);
                Fixed(ref h, f, m, vs, MaterialChannel.Wood, 0.8f);
            }
            // A marigold pot on the sill, only where the whole pot clears the road.
            if (h.Det.Small && !flush && rng.Chance(0.12f) && Fits(ref h, f, uc - 0.55, uc - 0.25, v0, d + 0.25))
            {
                vs = m.VertexCount;
                double x, y, z;
                f.ToWorld(uc - 0.4, v0, d + 0.04, out x, out y, out z);
                PropKit.PottedPlant(m, x, y, z, 0.2, PropKit.Terracotta, BuildingGrammar.Foliage, BuildingGrammar.Marigold);
                Free(ref h, m, vs, MaterialChannel.Foliage);
            }
        }

        /// <summary>Gajhya: the small projecting lattice window under the eave, with its own little roof.</summary>
        private static void Gajhya(ref House h, ref Plot p, double uc, double gw, double v0, double gh, uint wood, uint carved, uint lattice, MeshData m)
        {
            if (gw < 0.5 || gh < 0.3) return;
            KitFrame f = h.F;
            double a = uc - 0.5 * gw, b = uc + 0.5 * gw;
            // The box and its little roof (0.16 m beyond it) keep out of the road below 4.5 m; with less than 0.1 m
            // free the gajhya is left out (it sits under the eave, so this is rare).
            double d = Allow(ref h, f, a - 0.12, b + 0.12, v0 - 0.1, 0.58) - 0.16;
            if (d < 0.1) return;
            int vs = m.VertexCount;
            int seg = h.Det.Segs > 0 ? 1 : 0;
            KitRound.BoxU(m, f, a - 0.08, b + 0.08, v0 - 0.1, v0, 0, d + 0.06, 0.025, seg, BoxFaces.All & ~BoxFaces.Back, carved);
            KitRound.BoxV(m, f, a - 0.06, a + 0.04, v0, v0 + gh, 0, d, 0.02, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, carved);
            KitRound.BoxV(m, f, b - 0.04, b + 0.06, v0, v0 + gh, 0, d, 0.02, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, carved);
            MeshKit.QuadLocal(m, f, a, v0, 0, a, v0, d, a, v0 + gh, d, a, v0 + gh, 0, -1, 0, 0, wood);
            MeshKit.QuadLocal(m, f, b, v0, 0, b, v0, d, b, v0 + gh, d, b, v0 + gh, 0, 1, 0, 0, wood);
            Fixed(ref h, f, m, vs, MaterialChannel.WoodCarved);
            vs = m.VertexCount;
            MeshKit.Panel(m, f, a + 0.04, v0, b - 0.04, v0 + gh, d - 0.06, BuildingGrammar.Interior);
            FacadeKit.Lattice(m, f, a + 0.04, v0, b - 0.04, v0 + gh, d - 0.03, h.Det.LatticePitch * 0.9, 0.03, 0, false, lattice);
            Fixed(ref h, f, m, vs, MaterialChannel.Wood);
            vs = m.VertexCount;
            double hv = v0 + gh;
            MeshKit.QuadLocal(m, f, a - 0.12, hv, d + 0.16, b + 0.12, hv, d + 0.16, b + 0.12, hv + 0.26, 0, a - 0.12, hv + 0.26, 0, 0, 1, 1, p.Roof);
            Fixed(ref h, f, m, vs, MaterialChannel.RoofTile);
            vs = m.VertexCount;
            MeshKit.QuadLocal(m, f, a - 0.12, hv, d + 0.16, b + 0.12, hv, d + 0.16, b + 0.12, hv + 0.26, 0, a - 0.12, hv + 0.26, 0, 0, -1, -0.5, wood);
            MeshKit.TriLocal(m, f, a - 0.12, hv, d + 0.16, a - 0.12, hv, 0, a - 0.12, hv + 0.26, 0, -1, 0, 0, wood);
            MeshKit.TriLocal(m, f, b + 0.12, hv, d + 0.16, b + 0.12, hv, 0, b + 0.12, hv + 0.26, 0, 1, 0, 0, wood);
            Fixed(ref h, f, m, vs, MaterialChannel.Wood, 0.8f);
        }

        /// <summary>The ground-floor shop of a Newar house: a timber dalan (posts, carved beam with ears) around
        /// openings that show a stocked shop, or wooden plank shutters, or a steel roller shutter.</summary>
        private static void NewarShop(ref House h, ref Plot p, GrammarRng rng, KitHole[] holes, int nh, double g0, MeshData m)
        {
            KitFrame f = h.F;
            int seg = h.Det.Segs > 0 ? 1 : 0;
            for (int i = 0; i < nh; i++)
            {
                KitHole o = holes[i];
                int mode = rng.Chance(0.62f) ? 0 : rng.Chance(0.5f) ? 1 : 2; // open, planks, roller shutter
                if (mode == 0) ShopInterior(ref h, ref p, rng.Fork(), o, 1.4, m);
                else if (mode == 1 && h.Det.Flat)
                {
                    FlatOpening(ref h, f, o, MeshColor.Scale(p.Wood, 1.1f), MaterialChannel.Wood, -1, 0, 0, MaterialChannel.Wood, false, m);
                }
                else if (mode == 1)
                {
                    int v0 = m.VertexCount;
                    FacadeKit.Reveal(m, f, o, 0, 0.18, MeshColor.Scale(p.Front, 0.9f), false);
                    Recess(ref h, f, m, v0, p.FrontCh, 0.18);
                    v0 = m.VertexCount;
                    int planks = Math.Max(3, (int)Math.Round((o.U1 - o.U0) / 0.22));
                    for (int k = 0; k < planks; k++)
                    {
                        double a = o.U0 + (o.U1 - o.U0) * k / planks, b = o.U0 + (o.U1 - o.U0) * (k + 1) / planks - 0.012;
                        KitRound.BoxV(m, f, a, b, o.V0, o.V1, -0.16, -0.12, 0.01, h.Det.Small ? 1 : 0, BoxFaces.Front, MeshColor.Scale(p.Wood, (k & 1) == 0 ? 1.15f : 1.0f));
                    }
                    Recess(ref h, f, m, v0, MaterialChannel.Wood, 0.18, 0.95f);
                }
                else RollerShutter(ref h, ref p, rng.Fork(), o, false, m);
                // Dalan posts at the opening edges and the carved beam, as deep as the road in front allows.
                int vp = m.VertexCount;
                double pd = Proud(ref h, f, o.U0 - 0.32, o.U1 + 0.32, 0, 0.12, 0.02);
                if (h.Det.Flat)
                {
                    MeshKit.Panel(m, f, o.U0 - 0.14, o.V0, o.U0, o.V1, 0.02, p.Wood);
                    MeshKit.Panel(m, f, o.U1, o.V0, o.U1 + 0.14, o.V1, 0.02, p.Wood);
                    MeshKit.Box(m, f, o.U0 - 0.32, o.U1 + 0.32, o.V1, o.V1 + 0.16, 0, pd, MeshColor.Scale(p.Wood, 1.12f), BoxFaces.Front | BoxFaces.Bottom);
                    Fixed(ref h, f, m, vp, MaterialChannel.WoodCarved);
                    continue;
                }
                KitRound.BoxV(m, f, o.U0 - 0.14, o.U0, o.V0, o.V1, -0.02, 0.83 * pd, 0.025, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, p.Wood);
                KitRound.BoxV(m, f, o.U1, o.U1 + 0.14, o.V0, o.V1, -0.02, 0.83 * pd, 0.025, seg, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, p.Wood);
                KitRound.BoxU(m, f, o.U0 - 0.32, o.U1 + 0.32, o.V1, o.V1 + 0.16, 0, pd, 0.03, seg, BoxFaces.All & ~BoxFaces.Back, MeshColor.Scale(p.Wood, 1.12f));
                Fixed(ref h, f, m, vp, MaterialChannel.WoodCarved);
            }
            Sign(ref h, ref p, rng.Fork(), p.FU0 + 0.25, p.FU1 - 0.25, holes[0].V1 + 0.2, false, m);
        }

        /// <summary>An open shop seen through its doorway: dark room, side walls, a counter and stocked shelves, goods
        /// hanging in the opening (bazaar lanes).</summary>
        private static void ShopInterior(ref House h, ref Plot p, GrammarRng rng, in KitHole o, double depth, MeshData m)
        {
            KitFrame f = h.F;
            // The room stays inside the house: a thin plot (or one with a lane behind it) gets a shallower shop.
            depth = Math.Min(depth, Math.Max(0.4, 0.5 * p.Depth));
            while (depth > 0.45 && h.Clear.Active && Math.Min(h.Clear.Free(f, o.U0, -depth), h.Clear.Free(f, o.U1, -depth)) < 0.05) depth *= 0.7;
            int v0 = m.VertexCount;
            uint inside = MeshColor.Lerp(p.Front, BuildingGrammar.Interior, 0.55f);
            if (h.Det.Flat)
            {
                // The dark shop and a band of stock on its shelves.
                FlatOpening(ref h, f, o, MeshColor.Scale(inside, 0.8f), MaterialChannel.Plaster, -1, 0, 0, MaterialChannel.Plaster, false, m);
                if (o.V1 - o.V0 > 1.6)
                {
                    v0 = m.VertexCount;
                    MeshKit.Panel(m, f, o.U0 + 0.12, o.V0 + 0.9, o.U1 - 0.12, o.V0 + 1.5, 0.01, BuildingGrammar.Cloth[rng.Int(0, BuildingGrammar.Cloth.Length - 1)]);
                    Fixed(ref h, f, m, v0, MaterialChannel.Paint, 0.85f);
                }
                return;
            }
            FacadeKit.Reveal(m, f, o, 0, depth, inside, false);
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -depth, MeshColor.Scale(inside, 0.85f));
            MeshKit.QuadLocal(m, f, o.U0, o.V0 + 0.01, 0, o.U1, o.V0 + 0.01, 0, o.U1, o.V0 + 0.01, -depth, o.U0, o.V0 + 0.01, -depth, 0, 1, 0, MeshColor.FromHex(0x8A7F72));
            Recess(ref h, f, m, v0, MaterialChannel.Plaster, depth, 0.9f);
            if (!h.Det.Props) return; // shelves stay down to drop level 3: an empty shop reads as a hole
            v0 = m.VertexCount;
            double w = o.U1 - o.U0;
            // Shelves on the back wall stocked with boxes and bolts of cloth (front faces: seen from the street).
            for (int sh = 0; sh < 3; sh++)
            {
                double y = o.V0 + 0.55 + 0.5 * sh;
                if (y > o.V1 - 0.25) break;
                MeshKit.Box(m, f, o.U0 + 0.1, o.U1 - 0.1, y - 0.03, y, -depth, -depth + 0.35, MeshColor.Scale(p.Wood, 1.2f), BoxFaces.Front | BoxFaces.Top);
                int items = Math.Max(2, (int)(w / 0.4));
                for (int i = 0; i < items; i++)
                {
                    double a = o.U0 + 0.14 + (w - 0.28) * i / items, b = a + (w - 0.28) / items - 0.04;
                    double ih = rng.Range(0.18f, 0.38f);
                    uint c = BuildingGrammar.Cloth[rng.Int(0, BuildingGrammar.Cloth.Length - 1)];
                    MeshKit.Box(m, f, a, b, y, y + ih, -depth + 0.05, -depth + 0.3, c, BoxFaces.Front | BoxFaces.Top);
                }
            }
            // A counter near the front.
            KitRound.BoxU(m, f, o.U0 + 0.15, o.U0 + Math.Min(w - 0.15, 0.6 * w), o.V0, o.V0 + 0.85, -0.75, -0.35, 0.03, 1, BoxFaces.All & ~BoxFaces.Back & ~BoxFaces.Bottom,
                          MeshColor.Scale(p.Wood, 1.3f));
            Recess(ref h, f, m, v0, MaterialChannel.Paint, depth, 0.85f);
            // Goods over the shop front (bazaar profiles): garments on a rod across the head, a few hung inside.
            if (h.Plan.Profile == StyleProfile.KathmanduCore || h.Plan.Profile == StyleProfile.Thamel || h.Plan.Profile == StyleProfile.Patan)
            {
                if (h.Det.Small && rng.Chance(0.75f)) HangingGoods(ref h, ref p, rng.Fork(), o.U0 - 0.05, o.U1 + 0.05, o.V1 + 0.25, m);
                if (h.Det.Small && rng.Chance(0.5f))
                {
                    var saved = h.F;
                    h.F = f.Offset(0, 0, -0.35);
                    h.F = new KitFrame(h.F.OX, f.OY, h.F.OZ, f.UX, f.UZ);
                    HangingGoods(ref h, ref p, rng.Fork(), o.U0 + 0.15, o.U1 - 0.15, o.V1 - 0.1, m);
                    h.F = saved;
                }
            }
        }

        /// <summary>Garments and bags hung on a rod across the head of a bazaar shop front (Asan, Indra Chowk, Thamel):
        /// two-sided panels a hand in front of the wall.</summary>
        private static void HangingGoods(ref House h, ref Plot p, GrammarRng rng, double u0, double u1, double v, MeshData m)
        {
            KitFrame f = h.F;
            // The rod and the goods hang 0.12-0.16 m proud down to about 1 m under it: only where the road leaves room.
            if (!Fits(ref h, f, u0, u1, v - 1.0, 0.18)) return;
            int v0 = m.VertexCount;
            FacadeKit.RodLocal(m, f, u0, v, 0.14, u1, v, 0.14, 0.012, 3, false, BuildingGrammar.SteelDark);
            double u = u0 + 0.05;
            while (u < u1 - 0.3)
            {
                double gw = rng.Range(0.32f, 0.5f), gh = rng.Range(0.5f, 0.95f), w = 0.12 + 0.04 * rng.Next();
                if (u + gw > u1) break;
                uint c = BuildingGrammar.Cloth[rng.Int(0, BuildingGrammar.Cloth.Length - 1)];
                // A garment: shoulders wider than the hem, sleeves as a wider top band.
                MeshKit.QuadLocal(m, f, u, v - 0.02, w, u + gw, v - 0.02, w, u + gw - 0.06, v - gh, w, u + 0.06, v - gh, w, 0, 0, 1, c);
                MeshKit.QuadLocal(m, f, u, v - 0.02, w - 0.01, u + gw, v - 0.02, w - 0.01, u + gw - 0.06, v - gh, w - 0.01, u + 0.06, v - gh, w - 0.01, 0, 0, -1, MeshColor.Scale(c, 0.8f));
                u += gw + rng.Range(0.02f, 0.12f);
            }
            Free(ref h, m, v0, MaterialChannel.Fabric, 0.95f);
        }

        /// <summary>A steel roller shutter in an opening: corrugated slats, a bottom bar with a lock, the drum box
        /// at the head; <paramref name="halfOpen"/> leaves the lower third open.</summary>
        private static void RollerShutter(ref House h, ref Plot p, GrammarRng rng, in KitHole o, bool halfOpen, MeshData m)
        {
            KitFrame f = h.F;
            const double Reveal = 0.16;
            int v0 = m.VertexCount;
            uint c = BuildingGrammar.Shutter[rng.Int(0, BuildingGrammar.Shutter.Length - 1)];
            double bottom = halfOpen ? o.V0 + 0.35 * (o.V1 - o.V0) : o.V0;
            if (h.Det.Flat)
            {
                // The shutter (and the dark gap under a half-open one) on the wall, the drum box over it.
                FlatOpening(ref h, f, new KitHole(o.U0, bottom, o.U1, o.V1), c, MaterialChannel.Metal, -1, 0, 0, MaterialChannel.Metal, false, m);
                if (halfOpen) FlatOpening(ref h, f, new KitHole(o.U0, o.V0, o.U1, bottom), BuildingGrammar.Interior, MaterialChannel.Plain, -1, 0, 0, MaterialChannel.Plain, false, m);
                v0 = m.VertexCount;
                double fd = Proud(ref h, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, 0.14, 0.03);
                MeshKit.Box(m, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, o.V1 + 0.3, 0, fd, MeshColor.Scale(c, 0.92f), BoxFaces.Front | BoxFaces.Bottom);
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                return;
            }
            FacadeKit.Reveal(m, f, o, 0, Reveal, MeshColor.Scale(p.Front, 0.9f), false);
            Recess(ref h, f, m, v0, p.FrontCh, Reveal);
            if (halfOpen)
            {
                var low = new KitHole(o.U0, o.V0, o.U1, bottom);
                v0 = m.VertexCount;
                MeshKit.Panel(m, f, low.U0, low.V0, low.U1, low.V1, -1.0, BuildingGrammar.Interior);
                Recess(ref h, f, m, v0, MaterialChannel.Plain, 1.0);
            }
            v0 = m.VertexCount;
            if (h.Det.Courses) FacadeKit.Corrugated(m, f, o.U0, bottom, o.U1, o.V1, -Reveal + 0.02, 0.15, 0.025, c);
            else MeshKit.Panel(m, f, o.U0, bottom, o.U1, o.V1, -Reveal + 0.03, c);
            MeshKit.Box(m, f, o.U0, o.U1, bottom, bottom + 0.07, -Reveal + 0.02, -Reveal + 0.06, MeshColor.Scale(c, 0.8f), BoxFaces.Front | BoxFaces.Top);
            if (h.Det.Small)
            {
                double mid = 0.5 * (o.U0 + o.U1);
                MeshKit.Box(m, f, mid - 0.05, mid + 0.05, bottom + 0.02, bottom + 0.13, -Reveal + 0.06, -Reveal + 0.1, BuildingGrammar.Brass, BoxFaces.Wall);
            }
            Recess(ref h, f, m, v0, MaterialChannel.Metal, Reveal, 0.95f);
            v0 = m.VertexCount;
            double bd = Proud(ref h, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, 0.14, 0.03);
            FacadeKit.Band(m, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, 0.32, bd, Math.Min(0.09, 0.45 * bd), h.Det.Lite ? 0 : 3, MeshColor.Scale(c, 0.92f));
            Fixed(ref h, f, m, v0, MaterialChannel.Metal);
        }

        /// <summary>A potted plant or two on the sills of a floor.</summary>
        private static void SillPlant(ref House h, ref Plot p, GrammarRng rng, KitHole[] holes, int nh, MeshData m)
        {
            KitFrame f = h.F;
            for (int i = 0; i < nh; i++)
            {
                if (!rng.Chance(0.5f)) continue;
                KitHole o = holes[i];
                if (!Fits(ref h, f, o.U0, o.U1, o.V0 - 0.1, 0.24)) continue;
                int v0 = m.VertexCount;
                double x, y, z;
                f.ToWorld(0.5 * (o.U0 + o.U1), o.V0 - 0.04, 0.12, out x, out y, out z);
                PropKit.PottedPlant(m, x, y, z, 0.18, PropKit.Terracotta, rng.Chance(0.5f) ? BuildingGrammar.Foliage : BuildingGrammar.FoliageLight,
                                    rng.Chance(0.6f) ? (rng.Chance(0.5f) ? BuildingGrammar.Marigold : MeshColor.FromHex(0xE8483A)) : 0u);
                Free(ref h, m, v0, MaterialChannel.Foliage);
            }
        }

        // =============================================================================================================
        // NEWAR_HYBRID upper floors
        // =============================================================================================================

        private static void HybridUpper(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            KitFrame f = h.F;
            double u0 = p.FU0, u1 = p.FU1, w = u1 - u0;
            if (p.Storeys <= 3)
            {
                // A short hybrid: the brick stops at the terrace parapet.
                double vTop = p.Top + p.Parapet;
                if (vTop > FloorBase(s, p, p.Storeys)) Wall(ref h, ref p, f, u0, u1, FloorBase(s, p, p.Storeys), vTop, s.Holes, 0, p.Front, p.FrontCh, m);
                return;
            }
            uint plaster = p.ExposedBrick ? MeshColor.Scale(p.Wall, 1.06f) : p.Front;
            MaterialChannel pch = p.ExposedBrick ? MaterialChannel.Brick : MaterialChannel.Paint;
            int windows = Math.Max(1, Math.Min(4, (int)Math.Round(w / 1.9)));
            bool balcony = rng.Chance(0.3f) && w >= 3.0;
            KitHole[] holes = s.Holes;
            for (int k = 3; k < p.Storeys; k++)
            {
                double b0 = FloorBase(s, p, k), b1 = FloorBase(s, p, k + 1);
                bool top = k == p.Storeys - 1;
                int nh = 0;
                bool bal = balcony && top;
                double bw = Math.Min(2.4, w - 0.8);
                for (int j = 0; j < windows; j++)
                {
                    double c = u0 + (j + 0.5) * w / windows;
                    if (bal && Math.Abs(c - (u0 + 0.5 * w)) < 0.5 * bw) continue;
                    holes[nh++] = new KitHole(c - 0.42, b0 + 0.75, c + 0.42, b0 + Math.Min(2.2, b1 - b0 - 0.35));
                }
                if (bal) holes[nh++] = new KitHole(u0 + 0.5 * w - 0.45, b0 + 0.02, u0 + 0.5 * w + 0.45, b0 + 2.1);
                Wall(ref h, ref p, f, u0, u1, b0, top ? p.Top + p.Parapet : b1, holes, nh, plaster, pch, m);
                for (int i = 0; i < nh; i++)
                {
                    bool door = bal && i == nh - 1;
                    ModernWindow(ref h, ref p, holes[i], door, !door && rng.Chance(0.4f), false, p.Wood, m);
                }
                if (bal) Balcony(ref h, ref p, rng.Fork(), u0 + 0.5 * w, bw, b0, 1.0, m);
                if (h.Det.Bands)
                {
                    int v0 = m.VertexCount;
                    FacadeKit.Band(m, f, u0, u1, b0 - 0.12, 0.14, 0.07, 0.03, 3, MeshColor.Scale(plaster, 0.85f));
                    Fixed(ref h, f, m, v0, pch);
                }
            }
            // The eave hood at the old eave line on struts (jhingati or blue CGI).
            Hood(ref h, ref p, rng.Fork(), f, u0, u1, FloorBase(s, p, 3), rng.Range(0.6f, 0.9f), p.TileHood, m);
        }

        /// <summary>A single-slope pent hood along the facade at height v (27°, jhingati or blue CGI) on 2-4 struts,
        /// projecting <paramref name="want"/> (clipped out of the road below 4.5 m; dropped when less than 0.25 m is
        /// free): the hybrid's old eave line and the tiled pent roof over the ground floor of Patan and Bhaktapur.</summary>
        private static void Hood(ref House h, ref Plot p, GrammarRng rng, in KitFrame f, double u0, double u1, double v, double want, bool tile, MeshData m)
        {
            double w = u1 - u0, drop = want * Math.Tan(27 * Math.PI / 180);
            double d = Allow(ref h, f, u0, u1, v - drop - 0.1, want);
            if (d < 0.25) return;
            // The struts reach down 0.85 m below the eave line: only where they clear the road too.
            bool struts = h.Det.Struts && Fits(ref h, f, u0, u1, v - 1.0, 0.18);
            drop = d * Math.Tan(27 * Math.PI / 180);
            uint hood = tile ? BuildingGrammar.JhingatiColour(h.Plan.Profile, ref rng) : MeshColor.FromHex(0x3D7CC9);
            int v0 = m.VertexCount;
            MeshKit.QuadLocal(m, f, u0 - 0.05, v - drop, d, u1 + 0.05, v - drop, d, u1 + 0.05, v + 0.05, 0, u0 - 0.05, v + 0.05, 0, 0, 1, 1, hood);
            if (tile && h.Det.Courses) HoodCourses(ref h, f, u0 - 0.05, u1 + 0.05, v + 0.05, v - drop, d, hood, m);
            Fixed(ref h, f, m, v0, tile ? MaterialChannel.RoofTile : MaterialChannel.Metal);
            v0 = m.VertexCount;
            MeshKit.QuadLocal(m, f, u0 - 0.05, v - drop, d, u1 + 0.05, v - drop, d, u1 + 0.05, v + 0.05, 0, u0 - 0.05, v + 0.05, 0, 0, -1, -1, BuildingGrammar.SalDark);
            MeshKit.Box(m, f, u0 - 0.05, u1 + 0.05, v - drop - 0.08, v - drop, d - 0.06, d, MeshColor.Scale(p.Wood, 1.1f), BoxFaces.Front | BoxFaces.Bottom);
            Fixed(ref h, f, m, v0, MaterialChannel.Wood, 0.75f);
            if (struts)
            {
                v0 = m.VertexCount;
                int count = Math.Max(2, Math.Min(4, (int)Math.Round(w / 1.6)));
                for (int k = 0; k < count; k++)
                {
                    double u = u0 + (k + 0.5) * w / count;
                    Strut(m, f, u, v - 0.85, 0.04, v - drop - 0.02, d - 0.12, 0.09, p.Wood, h.Det.Segs);
                }
                Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            }
        }

        /// <summary>Tile courses on a sloped hood: a step every 0.2 m down the slope.</summary>
        private static void HoodCourses(ref House h, in KitFrame f, double u0, double u1, double vTop, double vBottom, double d, uint c, MeshData m)
        {
            int rows = Math.Max(2, (int)(Math.Sqrt(d * d + (vTop - vBottom) * (vTop - vBottom)) / 0.2));
            for (int r = 1; r < rows; r++)
            {
                double t = (double)r / rows, w = d * t, v = vTop + (vBottom - vTop) * t;
                MeshKit.QuadLocal(m, f, u0, v, w, u1, v, w, u1, v + 0.035, w - 0.01, u0, v + 0.035, w - 0.01, 0, 0.3, 1, MeshColor.Scale(c, 0.85f));
            }
        }

        /// <summary>A tundal strut from a wall foot (u, vFoot, wFoot) up and out to the eave (vTop, wTop): a bevelled
        /// beam with a carved bulge and a bracket block at its foot.</summary>
        private static void Strut(MeshData m, in KitFrame f, double u, double vFoot, double wFoot, double vTop, double wTop, double t, uint c, int segs)
        {
            double x0, y0, z0, x1, y1, z1;
            f.ToWorld(u, vFoot, wFoot, out x0, out y0, out z0);
            f.ToWorld(u, vTop, wTop, out x1, out y1, out z1);
            double dx = x1 - x0, dy = y1 - y0, dz = z1 - z0, len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.1) return;
            dx /= len;
            dy /= len;
            dz /= len;
            // Section axes: b along the facade (U), c perpendicular to both.
            double bx = f.UX, by = 0, bz = f.UZ;
            double cx = dy * bz - dz * by, cy = dz * bx - dx * bz, cz = dx * by - dy * bx;
            double cl = Math.Sqrt(cx * cx + cy * cy + cz * cz);
            cx /= cl;
            cy /= cl;
            cz /= cl;
            double ht = 0.5 * t;
            KitRound.Prism(m, x0, y0, z0, dx, dy, dz, len, bx, by, bz, cx, cy, cz, -ht, ht, -ht, ht, 0.3 * ht, segs > 1 ? 1 : 0, KitRound.SidesAll, 0, c);
            if (segs > 1)
            {
                // The carved bulge two-thirds up the strut.
                double mx = x0 + dx * len * 0.6, my = y0 + dy * len * 0.6, mz = z0 + dz * len * 0.6;
                KitRound.Prism(m, mx, my, mz, dx, dy, dz, 0.22 * len, bx, by, bz, cx, cy, cz, -ht * 1.35, ht * 1.35, -ht * 1.35, ht * 1.35, 0.5 * ht, 0,
                               KitRound.SidesAll, 3, MeshColor.Scale(c, 1.15f));
            }
            MeshKit.Box(m, f, u - 0.09, u + 0.09, vFoot - 0.14, vFoot + 0.04, 0, wFoot + 0.12, MeshColor.Scale(c, 1.1f), BoxFaces.All & ~BoxFaces.Back & ~BoxFaces.Top);
        }

        // =============================================================================================================
        // MODERN_URBAN — Baneshwor, Koteshwor, Kalanki, Thamel, the metro
        // =============================================================================================================

        private static void ModernFront(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            KitFrame f = h.F;
            double u0 = p.FU0, u1 = p.FU1, w = u1 - u0;
            KitHole[] holes = s.Holes;
            bool thamel = h.Plan.Profile == StyleProfile.Thamel;
            bool commercial = p.Storeys >= 4 && w >= 6 && h.Plan.MainLane && rng.Chance(thamel ? 0.1f : 0.35f) &&
                              (h.Plan.Profile == StyleProfile.Metro || h.Plan.Profile == StyleProfile.BoudhaKora || h.Plan.Profile == StyleProfile.None);
            double g0 = FloorBase(s, p, 0);
            double g1 = p.Storeys > 1 ? FloorBase(s, p, 1) : p.Top + p.Parapet;
            uint trim = p.Trim;
            int nh = 0;

            // ---- Ground floor.
            if (p.Shop)
            {
                int bays = Math.Max(1, Math.Min(3, (int)Math.Round(w / 3.0)));
                double sh = Math.Min(g1 - g0 - 0.55, 2.6);
                for (int k = 0; k < bays; k++)
                {
                    double a = u0 + w * k / bays + (k == 0 ? 0.3 : 0.14), b = u0 + w * (k + 1) / bays - (k == bays - 1 ? 0.3 : 0.14);
                    if (b - a > 0.8) holes[nh++] = new KitHole(a, g0, b, g0 + sh);
                }
            }
            else
            {
                double dc = u0 + (w > 4 ? 0.5 * w : 0.35 * w);
                holes[nh++] = new KitHole(dc - 0.5, g0, dc + 0.5, g0 + 2.15);
                if (w > 3.6)
                    for (int side = 0; side < 2; side++)
                    {
                        double c = side == 0 ? u0 + 0.25 * (dc - 0.5 - u0) + 0.6 : u1 - 0.25 * (u1 - dc - 0.5) - 0.6;
                        if (Math.Abs(c - dc) < 1.3) continue;
                        holes[nh++] = new KitHole(c - 0.55, g0 + 0.9, c + 0.55, g0 + 2.2);
                    }
            }
            Wall(ref h, ref p, f, u0, u1, h.Base - h.Ground, g1, holes, nh, p.Front, p.FrontCh, m);
            if (p.Shop)
            {
                for (int i = 0; i < nh; i++)
                {
                    KitHole o = holes[i];
                    if (commercial && rng.Chance(0.7f)) GlassShopfront(ref h, ref p, o, m);
                    else if (rng.Chance(0.68f))
                    {
                        ShopInterior(ref h, ref p, rng.Fork(), o, 1.6, m);
                        int vb = m.VertexCount;
                        uint c = BuildingGrammar.Shutter[rng.Int(0, BuildingGrammar.Shutter.Length - 1)];
                        double bd = Proud(ref h, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, 0.14, 0.03);
                        FacadeKit.Band(m, f, o.U0 - 0.05, o.U1 + 0.05, o.V1 - 0.02, 0.32, bd, Math.Min(0.09, 0.45 * bd), h.Det.Lite ? 0 : 3, MeshColor.Scale(c, 0.92f));
                        Fixed(ref h, f, m, vb, MaterialChannel.Metal);
                    }
                    else RollerShutter(ref h, ref p, rng.Fork(), o, rng.Chance(0.3f), m);
                }
                Sign(ref h, ref p, rng.Fork(), u0 + 0.12, u1 - 0.12, holes[0].V1 + 0.32, commercial, m);
                if (rng.Chance(commercial ? 0.1f : 0.35f)) Awning(ref h, ref p, rng.Fork(), u0 + 0.15, u1 - 0.15, holes[0].V1 + 0.3, m);
            }
            else
            {
                ModernDoor(ref h, ref p, rng.Fork(), holes[0], m);
                for (int i = 1; i < nh; i++) ModernWindow(ref h, ref p, holes[i], false, rng.Chance(0.75f), h.Det.Bands, p.Wood, m);
            }

            // ---- Columns (pilasters) at the plot ends.
            if (p.Storeys > 1)
            {
                int v0 = m.VertexCount;
                uint col = p.ExposedBrick ? BuildingGrammar.Concrete : MeshColor.Scale(p.Front, 0.93f);
                double cd = Proud(ref h, f, u0, u1, 0, 0.07, 0.03);
                KitRound.BoxV(m, f, u0, u0 + 0.26, g0 - 0.1, p.Top + p.Parapet, 0, cd, 0.4 * cd, h.Det.Segs > 0 ? 1 : 0, BoxFaces.Front | BoxFaces.Right, col);
                KitRound.BoxV(m, f, u1 - 0.26, u1, g0 - 0.1, p.Top + p.Parapet, 0, cd, 0.4 * cd, h.Det.Segs > 0 ? 1 : 0, BoxFaces.Front | BoxFaces.Left, col);
                Fixed(ref h, f, m, v0, p.ExposedBrick ? MaterialChannel.Concrete : p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.FrontCh);
            }
            if (p.Storeys <= 1) return;

            // ---- Upper floors: cantilevered over the street where it is free (or 4.5 m up), else flush.
            double cant = 0;
            if (!commercial && rng.Chance(0.35f))
            {
                double want = rng.Range(0.5f, 0.9f);
                cant = Allow(ref h, f, u0, u1, g1 - 0.15, want);
                if (cant < 0.3) cant = 0;
            }
            KitFrame fu = cant > 0 ? f.Offset(0, 0, cant) : f;
            fu = new KitFrame(fu.OX, h.Ground, fu.OZ, f.UX, f.UZ);
            p.Cant = cant;
            if (cant > 0) CantileverShell(ref h, s, ref p, cant, m);

            if (commercial)
            {
                CommercialUpper(ref h, s, ref p, ref rng, fu, cant, m);
                return;
            }
            bool balconies = w >= 4.2 && rng.Chance(thamel ? 0.65f : 0.5f);
            bool fullBalcony = balconies && rng.Chance(0.3f);
            int windows = Math.Max(1, Math.Min(4, (int)Math.Round(w / 2.6)));
            bool grilles = rng.Chance(0.6f);
            bool core = h.Plan.Profile == StyleProfile.KathmanduCore || h.Plan.Profile == StyleProfile.Patan || h.Plan.Profile == StyleProfile.Thamel;
            uint frame = core && rng.Chance(0.55f) ? (rng.Chance(0.6f) ? BuildingGrammar.PaintedBrown : p.Wood)
                : rng.Chance(0.55f) ? BuildingGrammar.Aluminium : rng.Chance(0.5f) ? MeshColor.FromHex(0xFFFFFF) : p.Wood;
            double bw = fullBalcony ? w - 0.7 : Math.Min(2.8, w * 0.42);
            for (int k = 1; k < p.Storeys; k++)
            {
                double b0 = FloorBase(s, p, k), b1 = FloorBase(s, p, k + 1);
                bool top = k == p.Storeys - 1;
                double uc = u0 + 0.5 * w;
                nh = 0;
                bool bal = balconies && (k > 1 || rng.Chance(0.7f));
                for (int j = 0; j < windows; j++)
                {
                    double c = u0 + (j + 0.5) * w / windows;
                    // Windows stay behind a wide balcony; only the one the balcony door replaces goes.
                    if (bal && Math.Abs(c - uc) < Math.Min(0.5 * bw + 0.3, 1.2)) continue;
                    double ww = Math.Min(1.25, w / windows - 0.7);
                    if (ww < 0.5) continue;
                    holes[nh++] = new KitHole(c - 0.5 * ww, b0 + 0.85, c + 0.5 * ww, b0 + Math.Min(2.2, b1 - b0 - 0.5));
                }
                int doorAt = -1;
                if (bal)
                {
                    doorAt = nh;
                    holes[nh++] = new KitHole(uc - 0.45, b0 + 0.02, uc + 0.45, b0 + 2.15);
                }
                Wall(ref h, ref p, fu, u0, u1, k == 1 && cant > 0 ? b0 - 0.16 : b0, top ? p.Top + p.Parapet : b1, holes, nh, p.Front, p.FrontCh, m);
                for (int i = 0; i < nh; i++)
                    ModernWindow(ref h, ref p, holes[i], i == doorAt, i != doorAt && grilles, i != doorAt && h.Det.Bands, frame, m, fu);
                if (bal) Balcony(ref h, ref p, rng.Fork(), uc, bw, b0, rng.Range(0.9f, 1.2f), m, fu);
                if (h.Det.Bands)
                {
                    int v0 = m.VertexCount;
                    FacadeKit.Band(m, fu, u0 - 0.03, u1 + 0.03, b0 - 0.16, 0.16, Proud(ref h, fu, u0, u1, b0 - 0.16, 0.06, 0.03), 0.035, 0, trim);
                    Fixed(ref h, fu, m, v0, p.FrontCh == MaterialChannel.BrickGlazed || p.ExposedBrick ? MaterialChannel.Concrete : MaterialChannel.Paint);
                }
                // Thamel and bazaar lanes: blade signs on the lower upper floors (one or two per floor in Thamel).
                float blade = thamel ? 0.8f : h.Plan.Profile == StyleProfile.KathmanduCore ? 0.3f : h.Style.SignsMax > 1 ? 0.45f : 0.08f;
                if (k <= 4 && rng.Chance(blade)) BladeSign(ref h, ref p, rng.Fork(), rng.Chance(0.5f) ? u0 + 0.35 : u1 - 0.45, b0 + 0.25, m, fu);
                if (thamel && k <= 3 && w > 5 && rng.Chance(0.45f)) BladeSign(ref h, ref p, rng.Fork(), u0 + w * rng.Range(0.35f, 0.65f), b0 + 0.35, m, fu);
                GrammarRng plants = rng.Fork();
                if (h.Det.Small && plants.Chance(thamel ? 0.35f : 0.12f)) SillPlant(ref h, ref p, plants, holes, doorAt >= 0 ? doorAt : nh, m);
            }
        }

        /// <summary>The cantilevered upper storeys' shell: the soffit over the street and the two cheeks.</summary>
        private static void CantileverShell(ref House h, Scratch s, ref Plot p, double cant, MeshData m)
        {
            KitFrame f = h.F;
            double u0 = Math.Min(p.FU0, p.GU0), u1 = Math.Max(p.FU1, p.GU1), v0 = FloorBase(s, p, 1) - 0.16, v1 = p.Top + p.Parapet;
            int vs = m.VertexCount;
            MeshKit.QuadLocal(m, f, u0, v0, 0, u1, v0, 0, u1, v0, cant, u0, v0, cant, 0, -1, 0, MeshColor.Scale(p.Front, 0.9f));
            MeshKit.QuadLocal(m, f, u0, v0, 0, u0, v0, cant, u0, v1, cant, u0, v1, 0, -1, 0, 0, p.Wall);
            MeshKit.QuadLocal(m, f, u1, v0, 0, u1, v0, cant, u1, v1, cant, u1, v1, 0, 1, 0, 0, p.Wall);
            WallPaint(ref h, p.WallCh, 1f).Apply(m, vs);
            // Roof strip over the cantilever.
            vs = m.VertexCount;
            double t = p.Top;
            MeshKit.QuadLocal(m, f, u0, t, 0, u1, t, 0, u1, t, cant, u0, t, cant, 0, 1, 0, p.Roof);
            Free(ref h, m, vs, MaterialChannel.Concrete);
        }

        /// <summary>A modern door: steel or timber leaves with a fanlight, a step and a small canopy.</summary>
        private static void ModernDoor(ref House h, ref Plot p, GrammarRng rng, in KitHole d, MeshData m)
        {
            KitFrame f = h.F;
            int v0 = m.VertexCount;
            uint leaf = rng.Chance(0.5f) ? BuildingGrammar.SteelDark : rng.Chance(0.5f) ? MeshColor.FromHex(0x8C5A3C) : MeshColor.FromHex(0x2F6FB0);
            if (h.Det.Flat)
            {
                FlatOpening(ref h, f, new KitHole(d.U0, d.V0, d.U1, d.V1 - 0.35), leaf, MaterialChannel.Metal, -1, 0, 0, MaterialChannel.Metal, false, m);
                FlatOpening(ref h, f, new KitHole(d.U0, d.V1 - 0.33, d.U1, d.V1), BuildingGrammar.GlassTints[(int)(p.Seed % (uint)BuildingGrammar.GlassTints.Length)],
                            MaterialChannel.Glass, -1, 0, 0, MaterialChannel.Glass, false, m);
                DoorCanopy(ref h, ref p, f, d, m);
                return;
            }
            FacadeKit.Reveal(m, f, d, 0, 0.2, MeshColor.Scale(p.Front, 0.9f), false);
            Recess(ref h, f, m, v0, p.FrontCh, 0.2);
            v0 = m.VertexCount;
            double mid = 0.5 * (d.U0 + d.U1);
            if (h.Det.Lite)
            {
                MeshKit.Panel(m, f, d.U0, d.V0, d.U1, d.V1 - 0.35, -0.15, leaf);
                MeshKit.Panel(m, f, d.U0, d.V1 - 0.33, d.U1, d.V1, -0.16, BuildingGrammar.GlassTints[(int)(p.Seed % (uint)BuildingGrammar.GlassTints.Length)]);
                Recess(ref h, f, m, v0, MaterialChannel.Metal, 0.2, 0.95f);
                DoorCanopy(ref h, ref p, f, d, m);
                return;
            }
            KitRound.BoxV(m, f, d.U0, mid - 0.01, d.V0, d.V1 - 0.35, -0.18, -0.13, 0.012, 1, BoxFaces.Front, leaf);
            KitRound.BoxV(m, f, mid + 0.01, d.U1, d.V0, d.V1 - 0.35, -0.18, -0.13, 0.012, 1, BoxFaces.Front, leaf);
            MeshKit.Panel(m, f, d.U0, d.V1 - 0.33, d.U1, d.V1, -0.16, BuildingGrammar.GlassTints[(int)(p.Seed % (uint)BuildingGrammar.GlassTints.Length)]);
            if (h.Det.Small)
                for (int i = 0; i < 4; i++)
                {
                    double u = d.U0 + (d.U1 - d.U0) * (i + 0.5) / 4;
                    MeshKit.Box(m, f, u - 0.012, u + 0.012, d.V1 - 0.33, d.V1, -0.16, -0.13, BuildingGrammar.SteelDark, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
            Recess(ref h, f, m, v0, MaterialChannel.Metal, 0.2, 0.95f);
            // Step and a small concrete canopy.
            double sd = Allow(ref h, f, d.U0 - 0.2, d.U1 + 0.2, 0, 0.35);
            if (sd > 0.1)
            {
                v0 = m.VertexCount;
                KitRound.BoxU(m, f, d.U0 - 0.2, d.U1 + 0.2, h.Base - h.Ground, d.V0, -0.05, sd, 0.03, 1, BoxFaces.All & ~BoxFaces.Back & ~BoxFaces.Bottom, BuildingGrammar.Concrete);
                Free(ref h, m, v0, MaterialChannel.Concrete);
            }
            DoorCanopy(ref h, ref p, f, d, m);
        }

        /// <summary>The small concrete canopy over a modern door (0.6 m, clipped out of the road below 4.5 m): a rounded
        /// band at the full levels, a plain slab at the lite ones, so the door keeps it at every level.</summary>
        private static void DoorCanopy(ref House h, ref Plot p, in KitFrame f, in KitHole d, MeshData m)
        {
            double cd = Allow(ref h, f, d.U0 - 0.3, d.U1 + 0.3, d.V1 + 0.2, 0.6);
            if (cd <= 0.2) return;
            int v0 = m.VertexCount;
            if (h.Det.Lite) MeshKit.Box(m, f, d.U0 - 0.3, d.U1 + 0.3, d.V1 + 0.2, d.V1 + 0.3, 0, cd, p.Trim, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            else FacadeKit.Band(m, f, d.U0 - 0.3, d.U1 + 0.3, d.V1 + 0.2, 0.1, cd, 0.04, 3, p.Trim);
            Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
        }

        /// <summary>
        /// A modern window (or a balcony door): reveal, aluminium or timber frame with a mullion and transom, tinted
        /// glass, a projecting sill, a steel grille on many, and a concrete chhajja sunshade above (0.3-0.45 m,
        /// clipped out of the road below 4.5 m).
        /// </summary>
        private static void ModernWindow(ref House h, ref Plot p, in KitHole o, bool door, bool grille, bool chhajja, uint frame, MeshData m)
        {
            ModernWindow(ref h, ref p, o, door, grille, chhajja, frame, m, h.F);
        }

        private static void ModernWindow(ref House h, ref Plot p, in KitHole o, bool door, bool grille, bool chhajja, uint frame, MeshData m, in KitFrame f)
        {
            const double Reveal = 0.16;
            int v0 = m.VertexCount;
            if (h.Det.Flat)
            {
                // Glass on the wall with its mullion, and a sill board under a window.
                FlatOpening(ref h, f, o, BuildingGrammar.GlassTints[(int)((p.Seed >> 3) % (uint)BuildingGrammar.GlassTints.Length)], MaterialChannel.Glass, -1, 0, 0,
                            MaterialChannel.Glass, false, m);
                v0 = m.VertexCount;
                double mu = 0.5 * (o.U0 + o.U1);
                MeshKit.Panel(m, f, mu - 0.03, o.V0, mu + 0.03, o.V1, 0.012, frame);
                if (!door) MeshKit.Box(m, f, o.U0 - 0.06, o.U1 + 0.06, o.V0 - 0.06, o.V0, 0, Proud(ref h, f, o.U0, o.U1, o.V0 - 0.06, 0.06), p.Trim, BoxFaces.Front | BoxFaces.Top);
                Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                return;
            }
            FacadeKit.Reveal(m, f, o, 0, Reveal, p.ExposedBrick ? BuildingGrammar.Concrete : MeshColor.Scale(p.Front, 0.92f), !door);
            Recess(ref h, f, m, v0, p.ExposedBrick ? MaterialChannel.Concrete : p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.FrontCh, Reveal);
            uint glass = BuildingGrammar.GlassTints[(int)((p.Seed >> 3) % (uint)BuildingGrammar.GlassTints.Length)];
            v0 = m.VertexCount;
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -Reveal + 0.02, glass);
            Recess(ref h, f, m, v0, MaterialChannel.Glass, Reveal, 1f);
            if (h.Det.Lite)
            {
                // Mullion and transom over the glass, a sill board.
                v0 = m.VertexCount;
                double mz = -Reveal + 0.05, mu = 0.5 * (o.U0 + o.U1);
                MeshKit.Panel(m, f, mu - 0.03, o.V0, mu + 0.03, o.V1, mz, frame);
                if (!door && o.V1 - o.V0 > 1.1) MeshKit.Panel(m, f, o.U0, o.V1 - 0.41, o.U1, o.V1 - 0.35, mz, frame);
                Recess(ref h, f, m, v0, frame == p.Wood ? MaterialChannel.Wood : MaterialChannel.Metal, Reveal, 1f);
                if (!door)
                {
                    double ld = Allow(ref h, f, o.U0 - 0.06, o.U1 + 0.06, o.V0 - 0.06, 0.07);
                    if (ld > 0.02)
                    {
                        v0 = m.VertexCount;
                        MeshKit.Box(m, f, o.U0 - 0.06, o.U1 + 0.06, o.V0 - 0.06, o.V0, 0, ld, p.Trim, BoxFaces.Front | BoxFaces.Top);
                        Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                    }
                }
                return;
            }
            v0 = m.VertexCount;
            double fw = 0.05, fz = -Reveal + 0.05;
            MeshKit.Panel(m, f, o.U0, o.V0, o.U0 + fw, o.V1, fz, frame);
            MeshKit.Panel(m, f, o.U1 - fw, o.V0, o.U1, o.V1, fz, frame);
            MeshKit.Panel(m, f, o.U0 + fw, o.V1 - fw, o.U1 - fw, o.V1, fz, frame);
            MeshKit.Panel(m, f, o.U0 + fw, o.V0, o.U1 - fw, o.V0 + fw, fz, frame);
            double mid = 0.5 * (o.U0 + o.U1);
            MeshKit.Panel(m, f, mid - 0.025, o.V0 + fw, mid + 0.025, o.V1 - fw, fz, frame);
            if (!door && o.V1 - o.V0 > 1.1)
            {
                double tv = o.V1 - 0.38;
                MeshKit.Panel(m, f, o.U0 + fw, tv - 0.025, o.U1 - fw, tv + 0.025, fz, frame);
            }
            Recess(ref h, f, m, v0, frame == p.Wood ? MaterialChannel.Wood : MaterialChannel.Metal, Reveal, 1f);
            if (!door)
            {
                double sd = Allow(ref h, f, o.U0 - 0.06, o.U1 + 0.06, o.V0 - 0.06, 0.07);
                if (sd > 0.02)
                {
                    v0 = m.VertexCount;
                    FacadeKit.Ledge(m, f, o.U0 - 0.06, o.U1 + 0.06, o.V0 - 0.06, 0.06, sd, 0.02, 0, p.Trim);
                    Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                }
            }
            if (grille && h.Det.Grilles)
            {
                v0 = m.VertexCount;
                uint g = BuildingGrammar.Railing[(int)((p.Seed >> 5) % (uint)BuildingGrammar.Railing.Length)];
                int bars = Math.Max(3, (int)((o.U1 - o.U0) / 0.16));
                for (int i = 1; i < bars; i++)
                {
                    double u = o.U0 + (o.U1 - o.U0) * i / bars;
                    MeshKit.Box(m, f, u - 0.011, u + 0.011, o.V0, o.V1, -0.04, -0.02, g, BoxFaces.Front | BoxFaces.Left);
                }
                double hv = o.V0 + 0.45 * (o.V1 - o.V0);
                MeshKit.Box(m, f, o.U0, o.U1, hv - 0.012, hv + 0.012, -0.045, -0.015, g, BoxFaces.Front | BoxFaces.Top);
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
            }
            if (chhajja)
            {
                double want = 0.38, v = o.V1 + 0.1;
                double d = Allow(ref h, f, o.U0 - 0.2, o.U1 + 0.2, v, want);
                if (d >= 0.15)
                {
                    v0 = m.VertexCount;
                    FacadeKit.Ledge(m, f, o.U0 - 0.2, o.U1 + 0.2, v, 0.08, d, 0.035, 3, p.Trim);
                    Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                }
            }
        }

        /// <summary>A cantilevered balcony: a slab with a rounded edge and drip, a railing (steel pipes, painted bars,
        /// concrete balusters or a solid brick parapet) and pot plants; clipped to the free depth before a road below
        /// 4.5 m, and reduced to a French balcony (rail across the door) when nothing is left.</summary>
        private static void Balcony(ref House h, ref Plot p, GrammarRng rng, double uc, double bw, double floorV, double want, MeshData m)
        {
            Balcony(ref h, ref p, rng.Fork(), uc, bw, floorV, want, m, h.F);
        }

        private static void Balcony(ref House h, ref Plot p, GrammarRng rng, double uc, double bw, double floorV, double want, MeshData m, in KitFrame f)
        {
            if (bw < 1.0) return;
            double a = uc - 0.5 * bw, b = uc + 0.5 * bw;
            double d = Allow(ref h, f, a, b, floorV - 0.18, want);
            uint rail = BuildingGrammar.Railing[rng.Int(0, BuildingGrammar.Railing.Length - 1)];
            // Railing styles: 0 stainless pipes, 1 painted bars, 2 white concrete balusters (guest houses), 3 a solid
            // parapet, 4 a painted timber jali (old Patan and Kathmandu houses).
            int style = rng.Int(0, 3);
            if (h.Plan.Profile == StyleProfile.Thamel && rng.Chance(0.4f)) style = 2;
            if ((p.Arch == BuildingArchetype.NewarHybrid || h.Plan.Profile == StyleProfile.Patan) && rng.Chance(0.45f)) style = 4;
            int v0;
            if (d < 0.35)
            {
                // French balcony.
                v0 = m.VertexCount;
                double rw = Math.Max(0.025, Proud(ref h, f, uc - 0.5, uc + 0.5, floorV, 0.08) - 0.02);
                FacadeKit.RodLocal(m, f, uc - 0.5, floorV + 0.95, rw, uc + 0.5, floorV + 0.95, rw, 0.02, 4, true, rail);
                if (h.Det.Rails)
                    for (int i = 0; i <= 6; i++)
                    {
                        double u = uc - 0.48 + 0.96 * i / 6;
                        FacadeKit.RodLocal(m, f, u, floorV + 0.05, rw, u, floorV + 0.95, rw, 0.01, 3, false, rail);
                    }
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                return;
            }
            v0 = m.VertexCount;
            uint slab = p.ExposedBrick ? BuildingGrammar.Concrete : p.Trim;
            if (h.Det.Lite)
            {
                // A slab and a railing panel: painted bars read as a darker screen at 30 m.
                MeshKit.Box(m, f, a, b, floorV - 0.16, floorV, 0, d, slab, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                v0 = m.VertexCount;
                uint screen = style == 3 ? p.Front : style == 2 ? MeshColor.FromHex(0xF2EFE8) : MeshColor.Scale(rail, 1.1f);
                MeshKit.QuadLocal(m, f, a, floorV, d - 0.03, b, floorV, d - 0.03, b, floorV + 1.0, d - 0.03, a, floorV + 1.0, d - 0.03, 0, 0, 1, screen);
                MeshKit.QuadLocal(m, f, a, floorV, 0, a, floorV, d - 0.03, a, floorV + 1.0, d - 0.03, a, floorV + 1.0, 0, -1, 0, 0, screen);
                MeshKit.QuadLocal(m, f, b, floorV, 0, b, floorV, d - 0.03, b, floorV + 1.0, d - 0.03, b, floorV + 1.0, 0, 1, 0, 0, screen);
                Fixed(ref h, f, m, v0, style == 3 ? (p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.FrontCh) : style == 2 ? MaterialChannel.Concrete : MaterialChannel.Metal);
                return;
            }
            FacadeKit.Band(m, f, a, b, floorV - 0.16, 0.16, d, 0.05, 3, slab);
            Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
            double top = floorV + 1.0;
            v0 = m.VertexCount;
            if (style == 4 && h.Det.Rails)
            {
                // Timber balcony: posts, rails and a painted lattice panel (teal, green or dark wood), cloth on the rail.
                uint jali = rng.Chance(0.5f) ? MeshColor.FromHex(0x3FA39A) : rng.Chance(0.5f) ? BuildingGrammar.PaintedGreen : p.Wood;
                double wr = d - 0.04;
                MeshKit.Box(m, f, a, b, floorV + 0.08, top - 0.06, wr - 0.02, wr, MeshColor.Scale(jali, 0.55f), BoxFaces.Front);
                Fixed(ref h, f, m, v0, MaterialChannel.Wood, 0.8f);
                v0 = m.VertexCount;
                FacadeKit.Lattice(m, f, a + 0.05, floorV + 0.1, b - 0.05, top - 0.1, wr + 0.012, 0.13, 0.03, 0, true, jali);
                int posts = Math.Max(2, (int)Math.Ceiling(bw / 0.9) + 1);
                for (int i = 0; i < posts; i++)
                {
                    double u = a + 0.03 + (bw - 0.06) * i / (posts - 1);
                    MeshKit.Box(m, f, u - 0.04, u + 0.04, floorV, top, wr - 0.04, wr + 0.03, jali, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                FacadeKit.Ledge(m, f, a - 0.02, b + 0.02, top - 0.06, 0.08, wr + 0.05, 0.03, 3, jali);
                MeshKit.QuadLocal(m, f, a, floorV, 0, a, floorV, wr, a, top, wr, a, top, 0, -1, 0, 0, MeshColor.Scale(jali, 0.85f));
                MeshKit.QuadLocal(m, f, b, floorV, 0, b, floorV, wr, b, top, wr, b, top, 0, 1, 0, 0, MeshColor.Scale(jali, 0.85f));
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                if (h.Det.Small && rng.Chance(0.4f))
                {
                    // A sari or a blanket airing over the rail.
                    v0 = m.VertexCount;
                    double ca = a + bw * rng.Range(0.1f, 0.5f), cb = Math.Min(b - 0.05, ca + rng.Range(0.6f, 1.2f));
                    uint cloth = BuildingGrammar.Cloth[rng.Int(0, BuildingGrammar.Cloth.Length - 1)];
                    MeshKit.QuadLocal(m, f, ca, top + 0.02, wr + 0.06, cb, top + 0.02, wr + 0.06, cb, top - 0.9, wr + 0.06, ca, top - 0.9, wr + 0.06, 0, 0, 1, cloth);
                    MeshKit.QuadLocal(m, f, ca, top + 0.02, wr + 0.05, cb, top + 0.02, wr + 0.05, cb, top - 0.9, wr + 0.05, ca, top - 0.9, wr + 0.05, 0, 0, -1,
                                      MeshColor.Scale(cloth, 0.8f));
                    Free(ref h, m, v0, MaterialChannel.Fabric);
                }
            }
            else if (style == 3 || !h.Det.Rails)
            {
                // Solid parapet (painted), with a rounded coping.
                MeshKit.QuadLocal(m, f, a, floorV, d, b, floorV, d, b, top - 0.06, d, a, top - 0.06, d, 0, 0, 1, p.Front);
                MeshKit.QuadLocal(m, f, a, floorV, 0, a, floorV, d, a, top - 0.06, d, a, top - 0.06, 0, -1, 0, 0, p.Front);
                MeshKit.QuadLocal(m, f, b, floorV, 0, b, floorV, d, b, top - 0.06, d, b, top - 0.06, 0, 1, 0, 0, p.Front);
                MeshKit.QuadLocal(m, f, a, floorV, d - 0.1, b, floorV, d - 0.1, b, top - 0.06, d - 0.1, a, top - 0.06, d - 0.1, 0, 0, -1, MeshColor.Scale(p.Front, 0.9f));
                Fixed(ref h, f, m, v0, p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.Paint : p.FrontCh);
                v0 = m.VertexCount;
                FacadeKit.Band(m, f, a - 0.03, b + 0.03, top - 0.08, 0.08, d + 0.03, 0.03, 3, slab);
                Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
            }
            else
            {
                double wr = d - 0.05;
                // Posts at the corners and every 1.2 m, a top rail on three sides.
                int posts = Math.Max(2, (int)Math.Ceiling(bw / 1.2) + 1);
                for (int i = 0; i < posts; i++)
                {
                    double u = a + 0.04 + (bw - 0.08) * i / (posts - 1);
                    FacadeKit.RodLocal(m, f, u, floorV, wr, u, top, wr, 0.025, 4, false, rail);
                }
                FacadeKit.RodLocal(m, f, a + 0.04, top, wr, b - 0.04, top, wr, 0.028, 5, true, rail);
                FacadeKit.RodLocal(m, f, a + 0.04, top, 0, a + 0.04, top, wr, 0.025, 4, true, rail);
                FacadeKit.RodLocal(m, f, b - 0.04, top, 0, b - 0.04, top, wr, 0.025, 4, true, rail);
                if (style == 0)
                {
                    // Stainless steel: three horizontal pipes.
                    for (int i = 1; i <= 3; i++)
                    {
                        double v = floorV + 0.25 * i;
                        FacadeKit.RodLocal(m, f, a + 0.04, v, wr, b - 0.04, v, wr, 0.014, 4, false, MeshColor.FromHex(0xD7DCE0));
                    }
                    Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                }
                else if (style == 1)
                {
                    // Painted mild-steel bars.
                    int bars = Math.Max(4, (int)(bw / 0.12));
                    for (int i = 1; i < bars; i++)
                    {
                        double u = a + bw * i / bars;
                        MeshKit.Box(m, f, u - 0.01, u + 0.01, floorV + 0.05, top, wr - 0.01, wr + 0.01, rail, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    }
                    FacadeKit.RodLocal(m, f, a + 0.04, floorV + 0.1, wr, b - 0.04, floorV + 0.1, wr, 0.014, 4, false, rail);
                    Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                }
                else
                {
                    // Concrete bottle balusters with a top rail slab.
                    Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                    v0 = m.VertexCount;
                    int count = Math.Max(3, (int)(bw / 0.24));
                    double[] r = KitRound.ProfileR, y = KitRound.ProfileY;
                    for (int i = 0; i < count; i++)
                    {
                        double u = a + bw * (i + 0.5) / count, x, yy, z;
                        f.ToWorld(u, floorV, wr, out x, out yy, out z);
                        int k = 0;
                        r[k] = 0.06;
                        y[k++] = 0;
                        r[k] = 0.06;
                        y[k++] = 0.1;
                        r[k] = 0.085;
                        y[k++] = 0.32;
                        r[k] = 0.04;
                        y[k++] = 0.62;
                        r[k] = 0.06;
                        y[k++] = 0.78;
                        r[k] = 0.06;
                        y[k++] = 0.86;
                        KitRound.LatheScratch(m, x, yy, z, k, 6, MeshColor.FromHex(0xF2EFE8), 45);
                    }
                    FacadeKit.Band(m, f, a, b, top - 0.12, 0.12, d + 0.02, 0.03, 3, MeshColor.FromHex(0xF2EFE8));
                    Fixed(ref h, f, m, v0, MaterialChannel.Concrete);
                }
            }
            if (h.Det.Small && rng.Chance(0.45f))
            {
                v0 = m.VertexCount;
                double x, y, z;
                f.ToWorld(a + 0.3, floorV, d - 0.25, out x, out y, out z);
                PropKit.PottedPlant(m, x, y, z, 0.28, PropKit.Terracotta, BuildingGrammar.Foliage, rng.Chance(0.5f) ? MeshColor.FromHex(0xE85D9E) : 0u);
                Free(ref h, m, v0, MaterialChannel.Foliage);
            }
        }

        /// <summary>A glass shop front of a commercial block: aluminium mullions, a door, tinted glass.</summary>
        private static void GlassShopfront(ref House h, ref Plot p, in KitHole o, MeshData m)
        {
            KitFrame f = h.F;
            int v0 = m.VertexCount;
            if (h.Det.Flat)
            {
                FlatOpening(ref h, f, o, MeshColor.FromHex(0x5E8DA8), MaterialChannel.Glass, -1, 0, 0, MaterialChannel.Glass, false, m);
                v0 = m.VertexCount;
                int n = Math.Max(2, (int)Math.Round((o.U1 - o.U0) / 1.1));
                for (int i = 1; i < n; i++)
                {
                    double u = o.U0 + (o.U1 - o.U0) * i / n;
                    MeshKit.Panel(m, f, u - 0.03, o.V0, u + 0.03, o.V1, 0.012, BuildingGrammar.Aluminium);
                }
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                return;
            }
            FacadeKit.Reveal(m, f, o, 0, 0.25, BuildingGrammar.Aluminium, false);
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -1.4, MeshColor.FromHex(0x5A5450));
            Recess(ref h, f, m, v0, MaterialChannel.Metal, 0.25);
            v0 = m.VertexCount;
            MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1, -0.2, MeshColor.FromHex(0x5E8DA8));
            Recess(ref h, f, m, v0, MaterialChannel.Glass, 0.25);
            v0 = m.VertexCount;
            int panes = Math.Max(2, (int)Math.Round((o.U1 - o.U0) / 1.1));
            for (int i = 0; i <= panes; i++)
            {
                double u = o.U0 + (o.U1 - o.U0) * i / panes;
                MeshKit.Box(m, f, u - 0.03, u + 0.03, o.V0, o.V1, -0.2, -0.14, BuildingGrammar.Aluminium, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            MeshKit.Box(m, f, o.U0, o.U1, o.V1 - 0.45, o.V1 - 0.4, -0.2, -0.14, BuildingGrammar.Aluminium, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            Fixed(ref h, f, m, v0, MaterialChannel.Metal);
        }

        /// <summary>The upper floors of a new commercial block: horizontal glass bands between aluminium-composite
        /// cladding (silver, blue, red), a big sign band.</summary>
        private static void CommercialUpper(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, in KitFrame fu, double cant, MeshData m)
        {
            double u0 = p.FU0, u1 = p.FU1, w = u1 - u0;
            uint acp = BuildingGrammar.Acp[rng.Int(0, BuildingGrammar.Acp.Length - 1)];
            uint glass = BuildingGrammar.GlassTints[rng.Int(0, BuildingGrammar.GlassTints.Length - 1)];
            KitHole[] holes = s.Holes;
            for (int k = 1; k < p.Storeys; k++)
            {
                double b0 = FloorBase(s, p, k), b1 = FloorBase(s, p, k + 1);
                bool top = k == p.Storeys - 1;
                int nh = 0;
                holes[nh++] = new KitHole(u0 + 0.4, b0 + 0.8, u1 - 0.4, b1 - 0.35);
                int v0 = m.VertexCount;
                FacadeKit.WallWithHoles(m, fu, Math.Min(u0, p.GU0), Math.Max(u1, p.GU1), b0, top ? p.Top + p.Parapet : b1, 0, holes, h.Det.Flat ? 0 : nh, acp);
                Fixed(ref h, fu, m, v0, MaterialChannel.Metal);
                KitHole o = holes[0];
                if (h.Det.Flat)
                {
                    FlatOpening(ref h, fu, o, glass, MaterialChannel.Glass, -1, 0, 0, MaterialChannel.Glass, false, m);
                    if (k == 1) Sign(ref h, ref p, rng.Fork(), u0 + 0.5, u1 - 0.5, b0 + 0.05, true, m, fu, 0.7);
                    continue;
                }
                v0 = m.VertexCount;
                FacadeKit.Reveal(m, fu, o, 0, 0.12, MeshColor.Scale(acp, 0.9f));
                MeshKit.Panel(m, fu, o.U0, o.V0, o.U1, o.V1, -0.1, glass);
                Recess(ref h, fu, m, v0, MaterialChannel.Glass, 0.12);
                v0 = m.VertexCount;
                int panes = Math.Max(2, (int)Math.Round((o.U1 - o.U0) / 1.2));
                for (int i = 1; i < panes; i++)
                {
                    double u = o.U0 + (o.U1 - o.U0) * i / panes;
                    MeshKit.Box(m, fu, u - 0.025, u + 0.025, o.V0, o.V1, -0.1, -0.05, BuildingGrammar.Aluminium, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                Fixed(ref h, fu, m, v0, MaterialChannel.Metal);
                if (k == 1) Sign(ref h, ref p, rng.Fork(), u0 + 0.5, u1 - 0.5, b0 + 0.05, true, m, fu, 0.7);
            }
        }

        // =============================================================================================================
        // Signs and awnings
        // =============================================================================================================

        /// <summary>
        /// Signboards over a shop front: one fascia board (a framed panel with abstract lettering: no names, no
        /// brands) or, in Thamel and the bazaar, 2-4 stacked and side-by-side boards. Depth is clipped out of the road.
        /// </summary>
        private static void Sign(ref House h, ref Plot p, GrammarRng rng, double u0, double u1, double v, bool big, MeshData m)
        {
            Sign(ref h, ref p, rng.Fork(), u0, u1, v, big, m, h.F, 0);
        }

        private static void Sign(ref House h, ref Plot p, GrammarRng rng, double u0, double u1, double v, bool big, MeshData m, in KitFrame f, double fixedH)
        {
            if (u1 - u0 < 0.8) return;
            int n = h.Style.SignsMax > 1 ? rng.Int(Math.Min(2, h.Style.SignsMin), Math.Min(4, h.Style.SignsMax)) : 1;
            if (big) n = 1;
            if (h.Det.Lite) n = Math.Min(n, 2);
            for (int k = 0; k < n; k++)
            {
                uint col = BuildingGrammar.Sign[rng.Int(0, BuildingGrammar.Sign.Length - 1)];
                uint ink = col == BuildingGrammar.Sign[4] || col == BuildingGrammar.Sign[1] ? MeshColor.FromHex(0x1E1E1E) : MeshColor.FromHex(0xFFFFFF);
                double hgt = fixedH > 0 ? fixedH : big ? 0.9 : rng.Range(0.55f, 0.85f), a = u0, b = u1;
                if (n > 1)
                {
                    double seg = (u1 - u0) / Math.Min(n, 2);
                    a = u0 + seg * (k % 2) + 0.05;
                    b = a + seg - 0.1;
                }
                double y = v + (k / 2) * 0.95;
                double d = Allow(ref h, f, a, b, y, 0.09);
                d = Math.Max(0.012, d);
                int v0 = m.VertexCount;
                MeshKit.Box(m, f, a, b, y, y + hgt, 0, d, col, h.Det.Lite ? BoxFaces.Front | BoxFaces.Bottom : BoxFaces.All & ~BoxFaces.Back);
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                v0 = m.VertexCount;
                if (h.Det.Lite)
                {
                    // One bar of "lettering" (abstract, no names).
                    MeshKit.Box(m, f, a + 0.15, b - 0.15, y + 0.35 * hgt, y + 0.62 * hgt, d, d + 0.006, ink, BoxFaces.Front);
                    Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                    continue;
                }
                // Border and lettering bars.
                MeshKit.Box(m, f, a + 0.04, b - 0.04, y + hgt - 0.08, y + hgt - 0.05, d, d + 0.006, ink, BoxFaces.Front);
                double len = b - a - 0.3;
                int words = Math.Max(1, (int)(len / 0.9));
                double x = a + 0.15;
                for (int i = 0; i < words; i++)
                {
                    double wl = len / words * rng.Range(0.55f, 0.85f);
                    MeshKit.Box(m, f, x, x + wl, y + 0.35 * hgt, y + 0.62 * hgt, d, d + 0.006, ink, BoxFaces.Front);
                    if (hgt > 0.6) MeshKit.Box(m, f, x, x + 0.6 * wl, y + 0.14 * hgt, y + 0.26 * hgt, d, d + 0.006, ink, BoxFaces.Front);
                    x += len / words;
                }
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
            }
        }

        /// <summary>A vertical blade sign on two arms (Thamel): 0.45 wide, 1.5-2.4 m tall, projecting 0.55-0.9 m;
        /// below 4.5 m it is clipped to the free depth or laid flat on the wall.</summary>
        private static void BladeSign(ref House h, ref Plot p, GrammarRng rng, double u, double v, MeshData m, in KitFrame f)
        {
            double tall = rng.Range(1.5f, 2.4f), want = rng.Range(0.55f, 0.9f);
            uint col = BuildingGrammar.Sign[rng.Int(0, BuildingGrammar.Sign.Length - 1)];
            double d = Allow(ref h, f, u - 0.05, u + 0.05, v, want);
            int v0 = m.VertexCount;
            if (h.Det.Lite)
            {
                // A thin blade: two faces and a front edge.
                if (d >= 0.4)
                {
                    MeshKit.QuadLocal(m, f, u - 0.03, v + 0.05, 0.12, u - 0.03, v + 0.05, d, u - 0.03, v + tall - 0.05, d, u - 0.03, v + tall - 0.05, 0.12, -1, 0, 0, col);
                    MeshKit.QuadLocal(m, f, u + 0.03, v + 0.05, 0.12, u + 0.03, v + 0.05, d, u + 0.03, v + tall - 0.05, d, u + 0.03, v + tall - 0.05, 0.12, 1, 0, 0, col);
                    MeshKit.Box(m, f, u - 0.03, u + 0.03, v + 0.05, v + tall - 0.05, d - 0.01, d, col, BoxFaces.Front);
                }
                else MeshKit.Box(m, f, u - 0.25, u + 0.25, v, v + tall, 0, 0.04, col, BoxFaces.Front | BoxFaces.Bottom);
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                return;
            }
            if (d < 0.4)
            {
                KitRound.BoxU(m, f, u - 0.25, u + 0.25, v, v + tall, 0, 0.04, 0.015, 1, BoxFaces.All & ~BoxFaces.Back, col);
                Fixed(ref h, f, m, v0, MaterialChannel.Paint);
                return;
            }
            KitRound.BoxW(m, f, u - 0.03, u + 0.03, v + 0.05, v + tall - 0.05, 0.12, d, 0.02, 1, BoxFaces.All & ~BoxFaces.Back, col);
            Fixed(ref h, f, m, v0, MaterialChannel.Paint);
            v0 = m.VertexCount;
            uint ink = col == BuildingGrammar.Sign[4] || col == BuildingGrammar.Sign[1] ? MeshColor.FromHex(0x1E1E1E) : MeshColor.FromHex(0xFFFFFF);
            for (int side = -1; side <= 1; side += 2)
            {
                double uu = u + side * 0.031;
                int letters = (int)(tall / 0.32);
                for (int i = 0; i < letters; i++)
                {
                    double vv = v + 0.2 + i * 0.3;
                    MeshKit.QuadLocal(m, f, uu, vv, 0.25, uu, vv, d - 0.12, uu, vv + 0.18, d - 0.12, uu, vv + 0.18, 0.25, side, 0, 0, ink);
                }
            }
            FacadeKit.RodLocal(m, f, u, v + tall - 0.1, 0, u, v + tall - 0.1, 0.14, 0.018, 4, false, BuildingGrammar.SteelDark);
            FacadeKit.RodLocal(m, f, u, v + 0.15, 0, u, v + 0.15, 0.14, 0.018, 4, false, BuildingGrammar.SteelDark);
            Fixed(ref h, f, m, v0, MaterialChannel.Paint);
        }

        /// <summary>An awning over the shop front: tin or canvas sloping out 0.6-1.0 m with a scalloped valance,
        /// clipped to the free depth (dropped when less than 0.25 m is free).</summary>
        private static void Awning(ref House h, ref Plot p, GrammarRng rng, double u0, double u1, double v, MeshData m)
        {
            KitFrame f = h.F;
            double want = rng.Range(0.6f, 1.0f), drop = 0.35 * want;
            double d = Allow(ref h, f, u0, u1, v - drop - 0.2, want);
            if (d < 0.25) return;
            drop = 0.35 * d;
            bool canvas = rng.Chance(0.5f);
            uint c = BuildingGrammar.Awning[rng.Int(0, BuildingGrammar.Awning.Length - 1)];
            int v0 = m.VertexCount;
            if (h.Det.Lite)
            {
                MeshKit.QuadLocal(m, f, u0, v - drop, d, u1, v - drop, d, u1, v, 0, u0, v, 0, 0, 1, 0.4, c);
                MeshKit.QuadLocal(m, f, u0, v - drop, d, u1, v - drop, d, u1, v, 0, u0, v, 0, 0, -1, -0.4, MeshColor.Scale(c, 0.7f));
                Fixed(ref h, f, m, v0, canvas ? MaterialChannel.Fabric : MaterialChannel.Metal);
                return;
            }
            if (canvas)
            {
                int stripes = Math.Max(2, (int)((u1 - u0) / 0.35));
                for (int i = 0; i < stripes; i++)
                {
                    double a = u0 + (u1 - u0) * i / stripes, b = u0 + (u1 - u0) * (i + 1) / stripes;
                    uint sc = (i & 1) == 0 ? c : MeshColor.FromHex(0xF4F1EA);
                    MeshKit.QuadLocal(m, f, a, v - drop, d, b, v - drop, d, b, v, 0, a, v, 0, 0, 1, 0.4, sc);
                    MeshKit.QuadLocal(m, f, a, v - drop, d, b, v - drop, d, b, v, 0, a, v, 0, 0, -1, -0.4, MeshColor.Scale(sc, 0.75f));
                    // Valance scallop.
                    MeshKit.TriLocal(m, f, a, v - drop, d, b, v - drop, d, 0.5 * (a + b), v - drop - 0.16, d, 0, 0, 1, sc);
                    MeshKit.TriLocal(m, f, a, v - drop, d, b, v - drop, d, 0.5 * (a + b), v - drop - 0.16, d, 0, 0, -1, MeshColor.Scale(sc, 0.75f));
                }
                Fixed(ref h, f, m, v0, MaterialChannel.Fabric);
            }
            else
            {
                double vtop = v, vbot = v - drop;
                // A corrugated tin sheet sloping out: ribs run down the slope.
                int ribs = Math.Max(3, (int)((u1 - u0) / 0.24));
                for (int i = 0; i < ribs; i++)
                {
                    double a = u0 + (u1 - u0) * i / ribs, b = u0 + (u1 - u0) * (i + 1) / ribs, mid = 0.5 * (a + b);
                    double ax, ay, az, bx, by, bz, mx, my, mz, ax2, ay2, az2, bx2, by2, bz2, mx2, my2, mz2;
                    f.ToWorld(a, vtop, 0, out ax, out ay, out az);
                    f.ToWorld(mid, vtop + 0.02, 0, out mx, out my, out mz);
                    f.ToWorld(b, vtop, 0, out bx, out by, out bz);
                    f.ToWorld(a, vbot, d, out ax2, out ay2, out az2);
                    f.ToWorld(mid, vbot + 0.02, d, out mx2, out my2, out mz2);
                    f.ToWorld(b, vbot, d, out bx2, out by2, out bz2);
                    double nx = 0.35 * f.WX, nz = 0.35 * f.WZ;
                    KitRound.QuadSmooth(m, ax, ay, az, mx, my, mz, mx2, my2, mz2, ax2, ay2, az2,
                                        nx - 0.5 * f.UX, 1, nz - 0.5 * f.UZ, nx, 1, nz, nx, 1, nz, nx - 0.5 * f.UX, 1, nz - 0.5 * f.UZ, c);
                    KitRound.QuadSmooth(m, mx, my, mz, bx, by, bz, bx2, by2, bz2, mx2, my2, mz2,
                                        nx, 1, nz, nx + 0.5 * f.UX, 1, nz + 0.5 * f.UZ, nx + 0.5 * f.UX, 1, nz + 0.5 * f.UZ, nx, 1, nz, c);
                }
                MeshKit.QuadLocal(m, f, u0, vbot - 0.005, d, u1, vbot - 0.005, d, u1, vtop - 0.005, 0, u0, vtop - 0.005, 0, 0, -1, -0.3, MeshColor.Scale(c, 0.7f));
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
                v0 = m.VertexCount;
                FacadeKit.RodLocal(m, f, u0 + 0.1, vbot + 0.02, d - 0.05, u0 + 0.1, vtop - 0.5, 0, 0.015, 4, false, BuildingGrammar.SteelDark);
                FacadeKit.RodLocal(m, f, u1 - 0.1, vbot + 0.02, d - 0.05, u1 - 0.1, vtop - 0.5, 0, 0.015, 4, false, BuildingGrammar.SteelDark);
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
            }
        }

        // =============================================================================================================
        // Corner (second street) facade
        // =============================================================================================================

        /// <summary>A face that is not the front: on a street (a corner house's second street, a side wall on a lane) shop
        /// bays or small windows on the ground floor, windows per floor with frames, sills and chhajjas (tikijhya on Newar
        /// floors), floor bands, signs; on open ground seen from a road (<paramref name="street"/> false) windows only,
        /// wider apart, with no shop and no sign. The counts and draws follow <paramref name="layoutLen"/> (the wall's length
        /// with square corners), so every drop level dresses the wall alike. Returns false when the edge is too short to
        /// dress.</summary>
        private static bool SideFacade(ref House h, Scratch s, ref Plot p, double ax, double az, double bx, double bz, double layoutLen, bool street, MeshData m)
        {
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            if (layoutLen < 2.5 || len < 2.0 || p.Arch == BuildingArchetype.RanaPalace) return false;
            var f = new KitFrame(ax, h.Ground, az, dx, dz);
            var rng = new GrammarRng(GrammarRng.Mix(p.Seed, (uint)Math.Round(layoutLen * 20)), 0x53494445);
            KitHole[] holes = s.Holes;
            bool modern = p.Arch == BuildingArchetype.ModernUrban;
            double spacing = modern ? 2.8 : 1.8;
            if (!street) spacing *= 1.35;
            int windows = Math.Max(1, Math.Min(5, (int)Math.Round(layoutLen / spacing)));
            bool shop = street && p.Shop && layoutLen >= 3.0 && rng.Chance(0.7f);
            var saved = h.F;
            h.F = f;
            for (int k = 0; k < p.Storeys; k++)
            {
                double b0 = k == 0 ? h.Base - h.Ground : FloorBase(s, p, k), b1 = k + 1 < p.Storeys ? FloorBase(s, p, k + 1) : p.Top + p.Parapet;
                double fb = FloorBase(s, p, k), fn = FloorBase(s, p, k + 1);
                bool newarFloor = !modern && (p.Arch == BuildingArchetype.Newar || k < 3);
                uint wallC = newarFloor || p.ExposedBrick ? p.Wall : p.Front;
                MaterialChannel ch = newarFloor ? p.WallCh : p.FrontCh;
                int nh = 0;
                if (k == 0)
                {
                    if (shop)
                    {
                        int bays = Math.Max(1, Math.Min(3, (int)Math.Round(layoutLen / 3.0)));
                        for (int j = 0; j < bays; j++)
                        {
                            double a = 0.3 + (len - 0.6) * j / bays + 0.1, b = 0.3 + (len - 0.6) * (j + 1) / bays - 0.1;
                            if (b - a + (layoutLen - len) / bays > 0.8) holes[nh++] = new KitHole(a, fb, b, fb + Math.Min(fn - fb - 0.5, 2.5));
                        }
                    }
                    else
                        for (int j = 0; j < windows; j++)
                        {
                            double c = 0.3 + (len - 0.6) * (j + 0.5) / windows;
                            holes[nh++] = new KitHole(c - 0.3, fb + 1.0, c + 0.3, fb + 1.75);
                        }
                }
                else
                    for (int j = 0; j < windows; j++)
                    {
                        double c = 0.3 + (len - 0.6) * (j + 0.5) / windows;
                        if (newarFloor) Tiki(holes, ref nh, c, fb, (len - 0.6) / windows, fn - fb);
                        else holes[nh++] = new KitHole(c - 0.55, fb + 0.85, c + 0.55, fb + Math.Min(2.2, fn - fb - 0.5));
                    }
                int v0 = m.VertexCount;
                Stats.SideOpenings += nh;
                FacadeKit.WallWithHoles(m, f, 0, len, b0, b1, 0, holes, h.Det.Flat ? 0 : nh, wallC);
                WallPaint(ref h, ch, 1f).Apply(m, v0);
                for (int i = 0; i < nh; i++)
                {
                    if (k == 0 && shop)
                    {
                        if (rng.Chance(0.6f)) ShopInterior(ref h, ref p, rng.Fork(), holes[i], 1.4, m);
                        else RollerShutter(ref h, ref p, rng.Fork(), holes[i], false, m);
                    }
                    else if (k == 0) SmallWindow(ref h, ref p, holes[i], m);
                    else if (newarFloor) Tikijhya(ref h, ref p, holes[i], p.Wood, MeshColor.Scale(p.Wood, 1.12f), MeshColor.Lerp(p.Wood, BuildingGrammar.SalMid, 0.5f), m);
                    else ModernWindow(ref h, ref p, holes[i], false, rng.Chance(0.5f), h.Det.Bands, BuildingGrammar.Aluminium, m, f);
                }
                // Signs: decided at every level (the lite level leaves them out), so the choices that follow stay put.
                GrammarRng signs = rng.Fork();
                if (k == 0 && shop && !h.Det.Lite) Sign(ref h, ref p, signs.Fork(), 0.3, len - 0.3, holes[0].V1 + 0.3, false, m, f, 0);
                if (street && k >= 1 && k <= 3 && !newarFloor &&
                    (h.Plan.Profile == StyleProfile.Thamel ? signs.Chance(0.6f) : h.Plan.Profile == StyleProfile.KathmanduCore && signs.Chance(0.25f)))
                {
                    double bu = signs.Chance(0.5f) ? 0.4 : len - 0.5;
                    if (!h.Det.Lite) BladeSign(ref h, ref p, signs.Fork(), bu, fb + 0.3, m, f);
                }
                if (k > 0 && h.Det.Bands)
                {
                    v0 = m.VertexCount;
                    double bd = Proud(ref h, f, 0, len, fb - 0.16, newarFloor ? 0.12 : 0.06, 0.03);
                    if (newarFloor) FacadeKit.Corbel(m, f, 0, len, fb - 0.16, 2, 0.11, 0.5 * bd, 3, MeshColor.Scale(p.Wall, 0.8f));
                    else FacadeKit.Band(m, f, 0, len, fb - 0.16, 0.16, bd, 0.035, 0, p.Trim);
                    Fixed(ref h, f, m, v0, newarFloor ? MaterialChannel.Brick : MaterialChannel.Paint);
                }
            }
            h.F = saved;
            return true;
        }

        // =============================================================================================================
        // RANA_PALACE
        // =============================================================================================================

        private static void RanaFront(ref House h, Scratch s, ref Plot p, ref GrammarRng rng, MeshData m)
        {
            KitFrame f = h.F;
            double u0 = p.FU0, u1 = p.FU1, w = u1 - u0;
            int bays = Math.Max(1, (int)Math.Round(w / 3.3));
            uint trim = BuildingGrammar.RanaTrim;
            KitHole[] holes = s.Holes;
            for (int k = 0; k < p.Storeys; k++)
            {
                double b0 = FloorBase(s, p, k), b1 = FloorBase(s, p, k + 1), sH = b1 - b0;
                bool top = k == p.Storeys - 1;
                int nh = 0;
                double wh = Math.Min(3.0, sH - 1.0);
                for (int bay = 0; bay < bays && nh < holes.Length; bay++)
                {
                    double uc = u0 + (bay + 0.5) * w / bays;
                    holes[nh++] = new KitHole(uc - 0.6, b0 + 0.5, uc + 0.6, b0 + 0.5 + wh);
                }
                Wall(ref h, ref p, f, u0, u1, k == 0 ? h.Base - h.Ground : b0, top ? p.Top + p.Parapet : b1, holes, nh, p.Front, MaterialChannel.Plaster, m);
                double rd = Proud(ref h, f, u0, u1, k == 0 ? 0 : b0 - 0.24, 0.12, 0.03), rk = rd / 0.12;
                for (int i = 0; i < nh; i++)
                {
                    KitHole o = holes[i];
                    int v0 = m.VertexCount;
                    if (h.Det.Flat)
                    {
                        // The green shutters under the fanlight, the architrave's head and sill.
                        FlatOpening(ref h, f, o, BuildingGrammar.RanaShutter, MaterialChannel.Wood, 0.14, 0.08, trim, MaterialChannel.Plaster, true, m);
                        v0 = m.VertexCount;
                        MeshKit.Panel(m, f, o.U0, o.V1 - 0.55, o.U1, o.V1, 0.01, MeshColor.FromHex(0x5E7F90));
                        Fixed(ref h, f, m, v0, MaterialChannel.Glass, 0.85f);
                        continue;
                    }
                    FacadeKit.Reveal(m, f, o, 0, 0.35, BuildingGrammar.RanaShadow);
                    MeshKit.Panel(m, f, o.U0, o.V0, o.U1, o.V1 - 0.55, -0.3, BuildingGrammar.RanaShutter);
                    Recess(ref h, f, m, v0, MaterialChannel.Plaster, 0.35);
                    v0 = m.VertexCount;
                    if (h.Det.Courses) FacadeKit.Corrugated(m, f, o.U0 + 0.05, o.V0 + 0.05, o.U1 - 0.05, o.V1 - 0.6, -0.28, 0.07, 0.012, BuildingGrammar.RanaShutter);
                    MeshKit.Panel(m, f, o.U0, o.V1 - 0.55, o.U1, o.V1, -0.25, MeshColor.FromHex(0x5E7F90));
                    Recess(ref h, f, m, v0, MaterialChannel.Wood, 0.35);
                    // Architrave, pediment cap and the fanlight bar.
                    v0 = m.VertexCount;
                    int rs = h.Det.Lite ? 0 : 1;
                    KitRound.BoxV(m, f, o.U0 - 0.14, o.U0, o.V0, o.V1, 0, 0.5 * rd, 0.02 * rk, rs, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, trim);
                    KitRound.BoxV(m, f, o.U1, o.U1 + 0.14, o.V0, o.V1, 0, 0.5 * rd, 0.02 * rk, rs, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, trim);
                    FacadeKit.Band(m, f, o.U0 - 0.22, o.U1 + 0.22, o.V1, 0.14, rd, 0.03 * rk, 3, trim);
                    MeshKit.QuadLocal(m, f, o.U0 - 0.2, o.V1 + 0.14, 0.83 * rd, o.U1 + 0.2, o.V1 + 0.14, 0.83 * rd, 0.5 * (o.U0 + o.U1), o.V1 + 0.42, 0.5 * rd,
                                      0.5 * (o.U0 + o.U1), o.V1 + 0.42, 0.5 * rd, 0, 0.3, 1, trim);
                    MeshKit.Box(m, f, o.U0, o.U1, o.V1 - 0.6, o.V1 - 0.55, -0.27, -0.22, trim, BoxFaces.Front | BoxFaces.Top);
                    Fixed(ref h, f, m, v0, MaterialChannel.Plaster);
                }
                // Pilasters between the bays and a cornice at each floor.
                int vp = m.VertexCount;
                for (int bay = 0; bay <= bays; bay++)
                {
                    double u = u0 + w * bay / bays;
                    KitRound.BoxV(m, f, Math.Max(u0, u - 0.22), Math.Min(u1, u + 0.22), k == 0 ? h.Base - h.Ground : b0, top ? p.Top : b1, 0, rd, 0.04 * rk,
                                  h.Det.Segs > 0 ? 1 : 0, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right, MeshColor.Scale(p.Front, 1.02f));
                }
                if (k > 0 && h.Det.Bands) FacadeKit.Corbel(m, f, u0 - 0.05, u1 + 0.05, b0 - 0.24, 2, 0.12, 0.65 * rd, 3, trim);
                Fixed(ref h, f, m, vp, MaterialChannel.Plaster);
            }
            // The top cornice; the balustrade is drawn by the roof pass.
            int vc = m.VertexCount;
            FacadeKit.Corbel(m, f, u0 - 0.2, u1 + 0.2, p.Top - 0.45, 3, 0.15, 0.15, 3, trim);
            Fixed(ref h, f, m, vc, MaterialChannel.Plaster);
        }
    }
}
