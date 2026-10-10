using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The second detail pass of the street facades (docs/research/w2/ref_buildings.md §12): the denser Newar window
    /// grid, carved toranas over doors and the best windows, the glazed dachi apa bands and lintel courses at the floor
    /// lines, the timber bhaygah balconies, a lime-washed ground floor, and the street dressing a Kathmandu facade wears
    /// (cable runs, laundry, potted plants, plaster patches and rain streaks on painted houses). Projections ask the
    /// corridor clearance like every other element (decision 2); the small dressing only exists at the richest level
    /// (<see cref="Detail.Small"/>), the structure (balconies) at every level.
    /// </summary>
    internal static partial class HouseBuilder
    {
        /// <summary>Newar bay count: <c>clamp(round(width / 1.3), 1, 9)</c>, odd from 4.5 m (a centre bay for the door and
        /// the sanjhya). A real Newar front has a window every 1.2-1.5 m on every floor (ref_buildings.md §12).</summary>
        private static int NewarBays(double width)
        {
            int n = (int)Math.Round(width / 1.3);
            n = n < 1 ? 1 : n > 9 ? 9 : n;
            if (width >= 4.5 && n % 2 == 0) n = n + 1 > 9 ? n - 1 : n + 1;
            return n;
        }

        /// <summary>
        /// A carved torana: the semicircular tympanum over a Newar door or window (ref_buildings.md §12), standing on the
        /// lintel at <paramref name="v"/>: a half-disc board <paramref name="halfW"/> wide each side and
        /// <paramref name="rise"/> high, with a rounded rim, a raised inner carving (gilt on some) and a small finial; as
        /// deep as the road in front allows (at least a relief of 1.5 cm). About 40 triangles.
        /// </summary>
        private static void Torana(ref House h, in KitFrame f, double uc, double halfW, double v, double rise, uint wood, bool gilt, MeshData m)
        {
            double depth = Proud(ref h, f, uc - halfW, uc + halfW, v, 0.07, 0.015);
            const int Seg = 8;
            Stats.Toranas++;
            int v0 = m.VertexCount;
            uint face = MeshColor.Scale(wood, 1.12f), rim = MeshColor.Scale(wood, 0.9f);
            for (int i = 0; i < Seg; i++)
            {
                double a0 = Math.PI * i / Seg, a1 = Math.PI * (i + 1) / Seg;
                double ua = uc + halfW * Math.Cos(a0), va = v + rise * Math.Sin(a0), ub = uc + halfW * Math.Cos(a1), vb = v + rise * Math.Sin(a1);
                // Front face (a fan from the centre of the base) and the rim from the wall out to the face.
                MeshKit.TriLocal(m, f, uc, v, depth, ub, vb, depth, ua, va, depth, 0, 0, 1, face);
                double nu = Math.Cos(0.5 * (a0 + a1)), nv = Math.Sin(0.5 * (a0 + a1));
                MeshKit.QuadLocal(m, f, ua, va, 0, ub, vb, 0, ub, vb, depth, ua, va, depth, nu, nv, 0.3, rim);
            }
            Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            // The inner carving: a smaller raised half-disc (the deity panel), gilt or carved, and the finial on top.
            v0 = m.VertexCount;
            double ih = 0.58 * halfW, ir = 0.62 * rise, id = depth + Math.Min(0.025, 0.4 * depth);
            uint inner = gilt ? BuildingGrammar.Brass : MeshColor.Scale(wood, 1.3f);
            for (int i = 0; i < Seg / 2; i++)
            {
                double a0 = Math.PI * i / (Seg / 2), a1 = Math.PI * (i + 1) / (Seg / 2);
                MeshKit.TriLocal(m, f, uc, v, id, uc + ih * Math.Cos(a1), v + ir * Math.Sin(a1), id, uc + ih * Math.Cos(a0), v + ir * Math.Sin(a0), id, 0, 0, 1, inner);
            }
            MeshKit.Box(m, f, uc - 0.035, uc + 0.035, v + rise - 0.01, v + rise + 0.12, 0, Math.Min(0.05, depth), face, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Fixed(ref h, f, m, v0, gilt ? MaterialChannel.Gilt : MaterialChannel.WoodCarved);
        }

        /// <summary>
        /// The glazed dachi apa band of a Newar floor line: three courses of dark glazed brick, 1 cm proud, just under
        /// the corbel (texture and colour from the BrickGlazed channel, not geometry: 2-4 triangles), and a lintel course
        /// at window-head height (<paramref name="lintelV"/>, NaN for none).
        /// </summary>
        private static void DachiApa(ref House h, ref Plot p, in KitFrame f, double u0, double u1, double v, double lintelV, MeshData m)
        {
            int v0 = m.VertexCount;
            uint glazed = MeshColor.Lerp(MeshColor.Scale(p.Front, 0.72f), MeshColor.FromHex(0x5A2A1E), 0.35f);
            double d = Proud(ref h, f, u0, u1, v - 0.4, 0.012, 0.006);
            MeshKit.Box(m, f, u0, u1, v - 0.4, v - 0.18, 0, d, glazed, BoxFaces.Front);
            Fixed(ref h, f, m, v0, MaterialChannel.BrickGlazed);
            if (double.IsNaN(lintelV)) return;
            v0 = m.VertexCount;
            double ld = Proud(ref h, f, u0, u1, lintelV, 0.04, 0.012);
            FacadeKit.Ledge(m, f, u0, u1, lintelV, 0.075, ld, 0.015, 0, MeshColor.Scale(p.Front, 0.86f));
            Fixed(ref h, f, m, v0, p.FrontCh == MaterialChannel.BrickGlazed ? MaterialChannel.BrickGlazed : MaterialChannel.Brick);
        }

        /// <summary>
        /// A bhaygah: the long timber balcony of an upper Newar floor (Patan, the Kathmandu core, Bhaktapur's squares):
        /// a plank deck on carved struts, posts about every metre, a top rail and a lattice screen (a square jali) across
        /// the front and the cheeks. Clipped to the free depth before a road below 4.5 m; dropped when less than 0.35 m is
        /// free. The structure is drawn at every level; the struts and the lattice thin out with the level; no strut stands
        /// in [<paramref name="skipU0"/>, <paramref name="skipU1"/>] (the sanjhya of the floor below).
        /// </summary>
        private static void Bhaygah(ref House h, ref Plot p, double u0, double u1, double floorV, double want, uint wood, MeshData m, double skipU0 = double.NaN,
                                    double skipU1 = double.NaN)
        {
            KitFrame f = h.F;
            double d = Allow(ref h, f, u0, u1, floorV - 0.9, want);
            if (d < 0.35)
            {
                d = Allow(ref h, f, u0, u1, floorV - 0.12, want);
                if (d < 0.35) return;
            }
            bool struts = h.Det.Struts && Fits(ref h, f, u0, u1, floorV - 0.9, d);
            Stats.Bhaygahs++;
            uint deck = MeshColor.Scale(wood, 1.08f), rail = MeshColor.Scale(wood, 1.15f), jali = MeshColor.Lerp(wood, BuildingGrammar.SalMid, 0.4f);
            int seg = h.Det.Segs > 0 ? 1 : 0;
            int v0 = m.VertexCount;
            // Deck and its carved fascia.
            KitRound.BoxU(m, f, u0, u1, floorV - 0.14, floorV + 0.02, 0, d, 0.02, seg, BoxFaces.All & ~BoxFaces.Back, deck);
            double railTop = floorV + 0.95;
            int posts = Math.Max(2, (int)Math.Round((u1 - u0) / 1.0) + 1);
            for (int i = 0; i < posts; i++)
            {
                double u = u0 + 0.04 + (u1 - u0 - 0.08) * i / (posts - 1);
                MeshKit.Box(m, f, u - 0.04, u + 0.04, floorV + 0.02, railTop, d - 0.1, d - 0.02, rail, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            KitRound.BoxU(m, f, u0, u1, railTop, railTop + 0.07, d - 0.12, d, 0.015, seg, BoxFaces.All & ~BoxFaces.Back, rail);
            // Cheek rails back to the wall.
            MeshKit.Box(m, f, u0, u0 + 0.06, railTop, railTop + 0.07, 0, d - 0.1, rail, BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            MeshKit.Box(m, f, u1 - 0.06, u1, railTop, railTop + 0.07, 0, d - 0.1, rail, BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Fixed(ref h, f, m, v0, MaterialChannel.Wood);
            // The jali screen across the front (a square lattice; plain balusters at the lightest levels).
            v0 = m.VertexCount;
            double pitch = h.Det.Lite ? 0.3 : Math.Max(0.11, 0.7 * h.Det.LatticePitch);
            FacadeKit.Lattice(m, f, u0 + 0.04, floorV + 0.08, u1 - 0.04, railTop - 0.04, d - 0.06, pitch, 0.03, h.Det.Lite ? 0 : 0.02, true, jali);
            Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved, 0.95f);
            if (struts)
            {
                v0 = m.VertexCount;
                int count = Math.Max(2, Math.Min(6, (int)Math.Round((u1 - u0) / 1.4)));
                for (int k = 0; k < count; k++)
                {
                    double u = u0 + (k + 0.5) * (u1 - u0) / count;
                    if (u > skipU0 && u < skipU1) continue;
                    Strut(m, f, u, floorV - 0.85, 0.04, floorV - 0.15, d - 0.1, 0.09, wood, h.Det.Segs);
                }
                Fixed(ref h, f, m, v0, MaterialChannel.WoodCarved);
            }
        }

        /// <summary>A lime-washed (or red-painted) ground floor band on a Newar brick front (Patan and Bhaktapur lanes):
        /// the wall from the plinth to <paramref name="v1"/> in lime or red over the same openings. Costs no more than the
        /// brick it replaces.</summary>
        private static uint LimeWash(ref House h, ref Plot p, GrammarRng rng)
        {
            float share = h.Plan.Profile == StyleProfile.Patan ? 0.3f : h.Plan.Profile == StyleProfile.Bhaktapur ? 0.18f :
                          h.Plan.Profile == StyleProfile.KathmanduCore ? 0.22f : 0.12f;
            if (!rng.Chance(share)) return 0;
            return rng.Chance(0.6f) ? BuildingGrammar.Lime : rng.Chance(0.5f) ? MeshColor.FromHex(0xA8352A) : MeshColor.FromHex(0xE6D9B8);
        }

        /// <summary>
        /// Street wiring on a facade (every Kathmandu lane): two or three black cables clipped to the wall at
        /// <paramref name="v"/>, sagging a little between the clips, and on some a drop to a meter box beside the door.
        /// They hug the wall (5-8 cm proud, clipped like any relief), so nothing hangs over the road. <paramref name="drop"/>
        /// false leaves out the drop to the meter box (a cantilevered wall that does not reach the ground).
        /// </summary>
        private static void FacadeCables(ref House h, in KitFrame f, double u0, double u1, double v, GrammarRng rng, MeshData m, bool drop = true)
        {
            if (u1 - u0 < 1.5) return;
            int wires = rng.Int(1, 3);
            int v0 = m.VertexCount;
            int spans = Math.Max(1, (int)Math.Round((u1 - u0) / 2.4));
            int pts = 2 * spans + 1;
            double[] px = Ensure3(pts);
            for (int k = 0; k < wires; k++)
            {
                double vk = v - 0.07 * k, w = Proud(ref h, f, u0, u1, vk - 0.1, 0.05 + 0.02 * k, 0.025);
                for (int i = 0; i < pts; i++)
                {
                    double t = (double)i / (pts - 1), u = u0 + (u1 - u0) * t;
                    double sag = (i & 1) == 1 ? 0.06 + 0.02 * k : 0;
                    double x, y, z;
                    f.ToWorld(u, vk - sag, w, out x, out y, out z);
                    _cx3[i] = x;
                    _cy3[i] = y;
                    _cz3[i] = z;
                }
                KitRound.Tube(m, _cx3, _cy3, _cz3, null, 0.011, pts, 3, false, MeshColor.FromHex(0x1C1C1E));
            }
            Fixed(ref h, f, m, v0, MaterialChannel.Rubber, 0.9f);
            if (rng.Chance(0.5f) && drop)
            {
                // A drop down the wall to a meter box at 1.8 m (not from a cantilevered wall).
                v0 = m.VertexCount;
                double ud = u0 + (u1 - u0) * rng.Range(0.15f, 0.35f), w = Proud(ref h, f, ud - 0.15, ud + 0.15, 1.6, 0.05, 0.025);
                double ax, ay, az, bx, by, bz;
                f.ToWorld(ud, v, w, out ax, out ay, out az);
                f.ToWorld(ud, 2.05, w, out bx, out by, out bz);
                KitRound.Rod(m, ax, ay, az, bx, by, bz, 0.012, 3, false, MeshColor.FromHex(0x1C1C1E));
                double bd = Proud(ref h, f, ud - 0.15, ud + 0.15, 1.55, 0.12, 0.03);
                KitRound.BoxU(m, f, ud - 0.14, ud + 0.14, 1.55, 2.05, 0, bd, 0.015, 0, BoxFaces.All & ~BoxFaces.Back, MeshColor.FromHex(0xD8D6CC));
                Fixed(ref h, f, m, v0, MaterialChannel.Metal);
            }
        }

        [ThreadStatic] private static double[] _cx3, _cy3, _cz3;

        private static double[] Ensure3(int n)
        {
            if (_cx3 == null || _cx3.Length < n)
            {
                int cap = Math.Max(16, n);
                _cx3 = new double[cap];
                _cy3 = new double[cap];
                _cz3 = new double[cap];
            }
            return _cx3;
        }

        /// <summary>Clothes drying on a line between two windows (or across one) of an upper floor, a hand's width off
        /// the wall: only where the road leaves room for them.</summary>
        private static void WindowLaundry(ref House h, in KitFrame f, double ua, double ub, double v, GrammarRng rng, MeshData m)
        {
            if (ub - ua < 0.9) return;
            if (!Fits(ref h, f, ua, ub, v - 1.0, 0.24)) return;
            int v0 = m.VertexCount;
            double ax, ay, az, bx, by, bz;
            f.ToWorld(ua, v, 0.2, out ax, out ay, out az);
            f.ToWorld(ub, v, 0.2, out bx, out by, out bz);
            PropKit.Laundry(m, ax, ay, az, bx, by, bz, rng.Int(2, 4), BuildingGrammar.Cloth, rng.NextUInt(), MeshColor.FromHex(0x2A2A2A));
            Free(ref h, m, v0, MaterialChannel.Fabric, 0.95f);
        }

        /// <summary>
        /// Weathering on a painted or plastered front (the metro, Asan, Thamel): two to four plaster patches of a slightly
        /// different paint, and grey rain streaks under the sills of <paramref name="holes"/>, each a quad 3-4 mm proud
        /// whose colour fades into the wall (vertex colours, no texture): 2 triangles each.
        /// </summary>
        private static void Weathering(ref House h, ref Plot p, in KitFrame f, double u0, double u1, double v0w, double v1w, KitHole[] holes, int nh, GrammarRng rng,
                                       MeshData m)
        {
            if (u1 - u0 < 1.2 || v1w - v0w < 1.0) return;
            int v0 = m.VertexCount;
            int patches = rng.Int(1, 3);
            for (int i = 0; i < patches; i++)
            {
                double pw = rng.Range(0.4f, 1.3f), ph = rng.Range(0.3f, 0.9f);
                double pu = u0 + 0.1 + (u1 - u0 - pw - 0.2) * rng.Next(), pv = v0w + 0.1 + (v1w - v0w - ph - 0.2) * rng.Next();
                if (pw > u1 - u0 - 0.2 || ph > v1w - v0w - 0.2) continue;
                bool hit = false;
                for (int k = 0; k < nh && !hit; k++)
                    hit = holes[k].U0 < pu + pw + 0.05 && holes[k].U1 > pu - 0.05 && holes[k].V0 < pv + ph + 0.05 && holes[k].V1 > pv - 0.05;
                if (hit) continue;
                uint c = rng.Chance(0.5f) ? MeshColor.Lerp(p.Front, 0xFFFFFFFFu, 0.22f) : MeshColor.Lerp(p.Front, BuildingGrammar.Concrete, 0.55f);
                MeshKit.Panel(m, f, pu, pv, pu + pw, pv + ph, 0.004, c);
            }
            Fixed(ref h, f, m, v0, p.FrontCh == MaterialChannel.Paint ? MaterialChannel.Plaster : p.FrontCh);
            v0 = m.VertexCount;
            uint streak = MeshColor.Lerp(p.Front, MeshColor.FromHex(0x4E4A44), 0.42f);
            for (int k = 0; k < nh; k++)
            {
                KitHole o = holes[k];
                if (o.V0 - 0.1 <= v0w + 0.3 || !rng.Chance(0.6f)) continue;
                double len = Math.Min(o.V0 - 0.1 - v0w, rng.Range(0.6f, 1.4f));
                StreakQuad(m, f, o.U0 + 0.05, o.U1 - 0.05, o.V0 - 0.1 - len, o.V0 - 0.08, 0.0035, streak, p.Front);
            }
            Fixed(ref h, f, m, v0, MaterialChannel.Plaster);
        }

        /// <summary>A quad on the wall whose top edge has colour <paramref name="top"/> and bottom edge
        /// <paramref name="bottom"/> (a stain that fades out downwards).</summary>
        private static void StreakQuad(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w, uint top, uint bottom)
        {
            double x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3;
            f.ToWorld(u0, v0, w, out x0, out y0, out z0);
            f.ToWorld(u1, v0, w, out x1, out y1, out z1);
            f.ToWorld(u1, v1, w, out x2, out y2, out z2);
            f.ToWorld(u0, v1, w, out x3, out y3, out z3);
            float nx = (float)f.WX, nz = (float)f.WZ;
            int a = m.AddVertex((float)x0, (float)y0, (float)z0, nx, 0, nz, bottom);
            int b = m.AddVertex((float)x1, (float)y1, (float)z1, nx, 0, nz, bottom);
            int c = m.AddVertex((float)x2, (float)y2, (float)z2, nx, 0, nz, top);
            int d = m.AddVertex((float)x3, (float)y3, (float)z3, nx, 0, nz, top);
            KitRound.TriOriented(m, a, b, c);
            KitRound.TriOriented(m, a, c, d);
        }
    }
}
