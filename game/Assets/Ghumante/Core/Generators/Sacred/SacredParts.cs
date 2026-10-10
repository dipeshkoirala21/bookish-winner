using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>The material looks of the sacred generators: palette tint + <see cref="MaterialChannel"/> (+ a default AO).
    /// Colours from docs/research/w2/ref_temples.md (photos of the real monuments, cartoon-boosted).</summary>
    internal static class Looks
    {
        public static readonly ShapeBrush PlinthBrick = new ShapeBrush(MeshColor.FromHex(0xAE5038), MaterialChannel.Brick);
        public static readonly ShapeBrush PlinthCoping = new ShapeBrush(MeshColor.FromHex(0xBF6B51), MaterialChannel.Brick);
        public static readonly ShapeBrush WallGlazed = new ShapeBrush(MeshColor.FromHex(0x9C3F2C), MaterialChannel.BrickGlazed);
        public static readonly ShapeBrush WoodDark = new ShapeBrush(MeshColor.FromHex(0x3E2416), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodMid = new ShapeBrush(MeshColor.FromHex(0x5E3822), MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodBeam = new ShapeBrush(MeshColor.FromHex(0x4A2C1C), MaterialChannel.Wood);
        public static readonly ShapeBrush Stone = new ShapeBrush(MeshColor.FromHex(0x8F8A82), MaterialChannel.Stone);
        public static readonly ShapeBrush StoneLight = new ShapeBrush(MeshColor.FromHex(0xA39A8C), MaterialChannel.Stone);
        public static readonly ShapeBrush StoneBuff = new ShapeBrush(MeshColor.FromHex(0xA59682), MaterialChannel.Stone);
        public static readonly ShapeBrush Gilt = new ShapeBrush(SacredPalette.Gilt, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltHi = new ShapeBrush(SacredPalette.GiltHi, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltAged = new ShapeBrush(MeshColor.FromHex(0xB58A2E), MaterialChannel.Gilt);
        public static readonly ShapeBrush Bronze = new ShapeBrush(MeshColor.FromHex(0xB08A3E), MaterialChannel.Metal);
        public static readonly ShapeBrush Silver = new ShapeBrush(SacredPalette.Silver, MaterialChannel.Metal);
        public static readonly ShapeBrush Tile = new ShapeBrush(MeshColor.FromHex(0x8E4A36), MaterialChannel.RoofTile);
        public static readonly ShapeBrush RidgeTile = new ShapeBrush(MeshColor.FromHex(0x5E2A1E), MaterialChannel.RoofTile);
        public static readonly ShapeBrush Lime = new ShapeBrush(MeshColor.FromHex(0xF1ECE0), MaterialChannel.Paint);
        public static readonly ShapeBrush Whitewash = new ShapeBrush(MeshColor.FromHex(0xF6F2E8), MaterialChannel.Plaster);
        public static readonly ShapeBrush Sindoor = new ShapeBrush(SacredPalette.Sindoor, MaterialChannel.Paint);
        public static readonly ShapeBrush PaintRed = new ShapeBrush(MeshColor.FromHex(0xC8282E), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintGreen = new ShapeBrush(MeshColor.FromHex(0x2E8B57), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintBlue = new ShapeBrush(MeshColor.FromHex(0x2F5DA8), MaterialChannel.Paint);
        public static readonly ShapeBrush PaintYellow = new ShapeBrush(MeshColor.FromHex(0xE8B923), MaterialChannel.Paint);
        public static readonly ShapeBrush Ink = new ShapeBrush(SacredPalette.EyesInk, MaterialChannel.Paint);
        public static readonly ShapeBrush Dark = new ShapeBrush(SacredPalette.SanctumDark, MaterialChannel.Plain);
        public static readonly ShapeBrush Glow = new ShapeBrush(SacredPalette.LampGlow, MaterialChannel.Plain);
        public static readonly ShapeBrush FringeRed = new ShapeBrush(MeshColor.FromHex(0xC8202A), MaterialChannel.Fabric);
        public static readonly ShapeBrush FringeWhite = new ShapeBrush(MeshColor.FromHex(0xF2EEE6), MaterialChannel.Fabric);
        public static readonly ShapeBrush ClothYellow = new ShapeBrush(MeshColor.FromHex(0xF7D21A), MaterialChannel.Fabric);
        public static readonly ShapeBrush Recess = new ShapeBrush(MeshColor.FromHex(0x2A1A12), MaterialChannel.Plain, 0.6f);

        public static ShapeBrush Of(uint col, MaterialChannel ch, float ao = 1f)
        {
            return new ShapeBrush(col, ch, ao);
        }

        public static ShapeBrush Ao(in ShapeBrush b, float ao)
        {
            return new ShapeBrush(b.Color, b.Channel, ao);
        }
    }

    /// <summary>
    /// Detailed, rounded building parts of the sacred generators (W2 detail pass; docs/research/w2/ref_temples.md):
    /// moulded plinth levels and stairs with balustrade walls, banded sanctum walls, carved door frames with ears,
    /// toranas, lattice windows and posts. All take a kit transform (local u across, v up, w out through the door).
    /// </summary>
    internal static partial class SacredParts
    {
        /// <summary>Segments of a round part at a LOD (LOD0 full, LOD1 two thirds, LOD2 a third; at least 3).</summary>
        public static int Seg(int lod0, int lod)
        {
            int s = lod <= 0 ? lod0 : lod == 1 ? (lod0 * 2 + 2) / 3 : (lod0 + 2) / 3;
            return Math.Max(3, s);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Plinths and walls
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// One plinth level from v0 to v1 round a plan outline (local u, w): a proud base course, the brick wall and a
        /// two-step coping that overhangs (ref_temples 1.1); the top is capped. <paramref name="below"/> sinks the base
        /// under the ground (first level). LOD2 draws a plain block.
        /// </summary>
        public static void PlinthLevel(MeshData m, in Affine3 k, double[] pu, double[] pw, int n, double v0, double v1, double below,
                                       in ShapeBrush wall, in ShapeBrush coping, int lod)
        {
            double rise = v1 - v0;
            Mould p = SacredDraw.M;
            if (lod >= 2 || rise < 0.12)
            {
                p.Add(0, v0 - below, wall).Add(0, v1, wall);
            }
            else
            {
                double baseH = Math.Min(0.2, 0.22 * rise), copH = Math.Min(0.24, 0.3 * rise), lip = Math.Min(0.12, 0.25 * rise);
                p.Add(0.05, v0 - below, coping).Add(0.05, v0 + baseH).Add(0, v0 + baseH, wall).Add(0, v1 - copH)
                 .Add(0.035, v1 - copH, coping).Add(0.035, v1 - 0.45 * copH).Add(lip, v1 - 0.45 * copH).Add(lip, v1);
            }
            SacredDraw.Ring(m, k, pu, pw, n, p);
            double o = p.O[p.Count - 1];
            SacredDraw.Cap(m, k, pu, pw, n, o * 1.2, v1, true, coping);
        }

        /// <summary>A rectangular plinth level of half sizes (hu, hw) (see <see cref="PlinthLevel"/>).</summary>
        public static void PlinthLevel(MeshData m, in Affine3 k, double hu, double hw, double v0, double v1, double below, in ShapeBrush wall,
                                       in ShapeBrush coping, int lod)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            PlinthLevel(m, k, pu, pw, n, v0, v1, below, wall, coping, lod);
        }

        /// <summary>A wall block of half sizes (hu, hw) from v0 to v1 with a carved wooden sill beam at the foot and a
        /// stepped wooden cornice under the eave (ref_temples 1.2). No top (a roof covers it).</summary>
        public static void BandedWall(MeshData m, in Affine3 k, double hu, double hw, double v0, double v1, in ShapeBrush wall, in ShapeBrush wood, int lod,
                                      bool sill = true, bool cornice = true)
        {
            Mould p = SacredDraw.M;
            double h = v1 - v0;
            if (lod >= 2 || h < 0.8)
            {
                p.Add(0, v0, wall).Add(0, v1, wall);
            }
            else
            {
                double s = Math.Min(0.28, 0.12 * h), c = Math.Min(0.55, 0.2 * h);
                if (sill) p.Add(0.06, v0, wood).Add(0.06, v0 + s).Add(0, v0 + s, wall);
                else p.Add(0, v0, wall);
                if (cornice)
                {
                    p.Add(0, v1 - c).Add(0.05, v1 - c, wood).Add(0.05, v1 - 0.62 * c).Add(0.11, v1 - 0.62 * c).Add(0.11, v1 - 0.3 * c).Add(0.17, v1 - 0.3 * c)
                     .Add(0.17, v1);
                }
                else
                {
                    p.Add(0, v1);
                }
            }
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(m, k, pu, pw, n, p);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Stairs
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A flight of stone steps along +W from <paramref name="wTop"/> (against the upper level) to wTop + run, rising
        /// from v0 at the foot to v1 at the top, between −halfW and +halfW, flanked by stepped balustrade walls of
        /// <paramref name="sideW"/> (0 = none). Adds a walkable ramp collider. Returns the steps.
        /// </summary>
        public static int Stair(MeshData m, GenColliders c, in KitFrame kf, in Affine3 k, double halfW, double wTop, double run, double v0, double v1,
                                double sideW, double sideH, in ShapeBrush step, in ShapeBrush side, int lod, byte material)
        {
            double rise = v1 - v0;
            if (rise <= 0.01 || run <= 0.01) return 0;
            int steps = Math.Max(1, (int)Math.Ceiling(rise / 0.26));
            double tread = run / steps, sr = rise / steps;
            if (lod >= 2)
            {
                SacredDraw.Quad(m, k, -halfW, v0, wTop + run, halfW, v0, wTop + run, halfW, v1, wTop, -halfW, v1, wTop, 0, run, rise, step);
            }
            else
            {
                ShapeBrush riser = Looks.Ao(step, 0.82f);
                for (int i = 0; i < steps; i++)
                {
                    double top = v0 + (i + 1) * sr, wa = wTop + run - (i + 1) * tread, wb = wTop + run - i * tread;
                    double nose = lod == 0 ? 0.025 : 0;
                    SacredDraw.Box(m, k, -halfW, halfW, top - sr, top, wa, wb + nose, riser, BoxFaces.Front);
                    SacredDraw.Box(m, k, -halfW, halfW, top - sr, top, wa, wb + nose, step, BoxFaces.Top);
                    if (sideW <= 0) SacredDraw.Box(m, k, -halfW, halfW, v0, top, wa, wb + nose, step, BoxFaces.Left | BoxFaces.Right);
                }
            }
            if (sideW > 0)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double ui = s * halfW, uo = s * (halfW + sideW);
                    double wb = wTop + run + 0.12, hb = v0 + sideH, ht = v1 + sideH;
                    // Outer and inner faces, the sloping top and the front end.
                    SacredDraw.Quad(m, k, uo, v0 - 0.2, wb, uo, hb, wb, uo, ht, wTop, uo, v0 - 0.2, wTop, s, 0, 0, side);
                    SacredDraw.Quad(m, k, ui, v0, wb, ui, hb, wb, ui, ht, wTop, ui, v0, wTop, -s, 0, 0, Looks.Ao(side, 0.85f));
                    SacredDraw.Quad(m, k, ui, hb, wb, uo, hb, wb, uo, ht, wTop, ui, ht, wTop, 0, run, rise, side);
                    SacredDraw.Quad(m, k, ui, v0 - 0.2, wb, uo, v0 - 0.2, wb, uo, hb, wb, ui, hb, wb, 0, 0, 1, side);
                }
            }
            if (c != null)
            {
                double x0, y0, z0, x1, y1, z1;
                kf.ToWorld(0, v0, wTop + run, out x0, out y0, out z0);
                kf.ToWorld(0, v1, wTop, out x1, out y1, out z1);
                c.AddRamp(new GenRamp { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Y0 = (float)y0, Y1 = (float)y1, HalfWidth = (float)halfW, Material = material });
            }
            return steps;
        }

        /// <summary>A moulded stone pedestal of half size h × d, height ph, at (u, v, w).</summary>
        public static void Pedestal(MeshData m, in Affine3 k, double u, double v, double w, double hu, double hw, double ph, in ShapeBrush b, int lod)
        {
            Affine3 x = SacredDraw.At(k, u, 0, w);
            Mould p = SacredDraw.M;
            if (lod >= 2) p.Add(0, v, b).Add(0, v + ph);
            else
                p.Add(0.05, v, b).Add(0.05, v + 0.18 * ph).Add(0, v + 0.18 * ph).Add(0, v + 0.78 * ph).Add(0.05, v + 0.78 * ph).Add(0.05, v + ph);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(m, x, pu, pw, n, p);
            SacredDraw.CapRect(m, x, 0, 0, hu + p.O[p.Count - 1], hw + p.O[p.Count - 1], v + ph, true, b);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Doors, toranas, windows, posts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A closed sanctum door on the wall plane w (facing +w): the opaque dark doorway with a lamp glow (never an
        /// interior), a carved frame of stepped jambs whose lintel and sill run past the jambs as "ears", and a gilt torana
        /// above (ref_temples 1.2), sized to stay under <paramref name="maxTop"/>. <paramref name="frame"/> is the frame look (dark wood,
        /// silver at Pashupati).
        /// Returns the top of the torana.
        /// </summary>
        public static double Door(MeshData m, in Affine3 k, double u, double v, double w, double dw, double dh, in ShapeBrush frame, int lod, bool torana = true,
                                  double maxTop = double.MaxValue)
        {
            SacredDraw.Panel(m, k, u - 0.5 * dw, v, u + 0.5 * dw, v + dh, w + 0.012, Looks.Dark);
            if (lod >= 2) return v + dh;
            SacredDraw.Panel(m, k, u - 0.07, v + 0.55, u + 0.07, v + 0.8, w + 0.016, Looks.Glow);
            int layers = lod == 0 ? 3 : lod == 1 ? 2 : 1;
            double jw = 0.11, top = v + dh;
            for (int j = 0; j < layers; j++)
            {
                double inner = 0.5 * dw + j * jw, outer = inner + jw, proud = 0.13 - 0.035 * j, ear = 0.2 + 0.1 * j;
                double lt = top + (j + 1) * jw;
                ShapeBrush b = j % 2 == 0 ? frame : Looks.Ao(frame, 0.9f);
                BoxFaces f = lod >= 2 ? BoxFaces.Front : BoxFaces.Wall;
                SacredDraw.Box(m, k, u - outer, u - inner, v, lt - jw, w, w + proud, b, f);
                SacredDraw.Box(m, k, u + inner, u + outer, v, lt - jw, w, w + proud, b, f);
                SacredDraw.Box(m, k, u - outer - ear, u + outer + ear, lt - jw, lt, w, w + proud, b, f | BoxFaces.Bottom);
                if (j == 0 && lod < 2) SacredDraw.Box(m, k, u - outer - ear, u + outer + ear, v - 0.02, v + 0.09, w, w + proud + 0.02, b, BoxFaces.Wall);
            }
            double tTop = top + layers * jw;
            // The torana fits under whatever is above the door (an eave, a cornice).
            double tw = Math.Min(dw + 0.6, 2 * (maxTop - tTop - 0.05) / 0.95);
            if (torana && lod < 3 && tw > 0.45) tTop = Torana(m, k, u, tTop + 0.02, w, tw, Looks.Gilt, lod);
            return tTop;
        }

        /// <summary>A gilt torana over a door centred at u: a thick semicircular tympanum with a slightly pointed crown, a
        /// raised rim, a central deity relief and the kirtimukha knob at the apex. Returns its top.</summary>
        public static double Torana(MeshData m, in Affine3 k, double u, double v, double w, double width, in ShapeBrush b, int lod)
        {
            double r = 0.5 * width, h = 0.78 * r;
            int seg = lod == 0 ? 10 : lod == 1 ? 6 : 4;
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            int n = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg;
                double px = r * Math.Cos(a), py = h * Math.Sin(a);
                // A gentle point at the crown.
                py += 0.12 * h * Math.Pow(Math.Sin(a), 8);
                x[n] = px;
                z[n] = -py;
                n++;
            }
            // Basis: x = u, extrusion y = +w (outward), z = x × y = −v.
            Affine3 f = SacredDraw.Basis(k, u, v, w, 1, 0, 0, 0, 0, 1);
            if (lod >= 2)
            {
                Shapes.BevelExtrude(m, f, b, x, z, n, 0.06, 0, 0, BevelStyle.Chamfer, true, false);
                return v + 1.12 * h;
            }
            Shapes.BevelExtrude(m, f, b, x, z, n, 0.07, 0.025, 1, BevelStyle.Round, true, false);
            // Raised rim and the relief.
            Affine3 rim = SacredDraw.Basis(k, u, v, w + 0.07, 1, 0, 0, 0, 0, 1);
            for (int i = 0; i < n; i++)
            {
                x[i] *= 0.82;
                z[i] *= 0.82;
            }
            if (lod == 0)
            {
                Shapes.BevelExtrude(m, rim, Looks.GiltHi, x, z, n, 0.03, 0.012, 1, BevelStyle.Round, true, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.42 * h, w + 0.1), Looks.GiltHi, 0.16 * r, 0.3 * h, 0.06, 8);
                Shapes.Sphere(m, SacredDraw.At(k, u, v + 0.74 * h, w + 0.11), Looks.GiltHi, 0.06 * r + 0.03, 6);
            }
            Shapes.Sphere(m, SacredDraw.At(k, u, v + 1.1 * h, w + 0.04), b, 0.05 + 0.05 * r, lod == 0 ? 6 : 4);
            return v + 1.12 * h + 0.05;
        }

        /// <summary>A carved lattice window (tiki jhya) centred at (u, v) on the wall plane w: a dark frame with ears, a
        /// recessed dark panel and a lattice grid of bars.</summary>
        public static void LatticeWindow(MeshData m, in Affine3 k, double u, double v, double w, double ww, double wh, in ShapeBrush frame, int lod, int bars = 3)
        {
            double fw = 0.08, hu = 0.5 * ww, hv = 0.5 * wh;
            SacredDraw.Panel(m, k, u - hu, v - hv, u + hu, v + hv, w + 0.01, Looks.Recess);
            BoxFaces f = lod >= 2 ? BoxFaces.Front : BoxFaces.Wall;
            SacredDraw.Box(m, k, u - hu - fw, u - hu, v - hv, v + hv, w, w + 0.09, frame, f);
            SacredDraw.Box(m, k, u + hu, u + hu + fw, v - hv, v + hv, w, w + 0.09, frame, f);
            SacredDraw.Box(m, k, u - hu - fw - 0.12, u + hu + fw + 0.12, v + hv, v + hv + fw, w, w + 0.1, frame, f | BoxFaces.Bottom);
            SacredDraw.Box(m, k, u - hu - fw - 0.12, u + hu + fw + 0.12, v - hv - fw, v - hv, w, w + 0.1, frame, f);
            if (lod >= 1) return;
            double bw = 0.025;
            for (int i = 1; i <= bars; i++)
            {
                double uu = u - hu + ww * i / (bars + 1), vv = v - hv + wh * i / (bars + 1);
                SacredDraw.Box(m, k, uu - bw, uu + bw, v - hv, v + hv, w + 0.01, w + 0.05, frame, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, k, u - hu, u + hu, vv - bw, vv + bw, w + 0.01, w + 0.05, frame, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            }
        }

        /// <summary>A carved wooden post from v0 to v1 at (u, w): a moulded square base, the shaft, a flared capital and a
        /// bracket (saddle) along the beam direction (local u).</summary>
        public static void Post(MeshData m, in Affine3 k, double u, double w, double v0, double v1, double t, in ShapeBrush wood, int lod)
        {
            double h = 0.5 * t;
            if (lod >= 1)
            {
                SacredDraw.Box(m, k, u - h, u + h, v0, v1, w - h, w + h, wood, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                return;
            }
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(u, w, h, h, pu, pw);
            Mould p = SacredDraw.M;
            p.Add(0.07, v0, wood).Add(0.07, v0 + 0.2).Add(0, v0 + 0.2).Add(0, v1 - 0.28).Add(0.05, v1 - 0.28).Add(0.05, v1);
            SacredDraw.Ring(m, k, pu, pw, n, p);
            if (lod == 0) SacredDraw.Box(m, k, u - 3.2 * h, u + 3.2 * h, v1, v1 + 0.12, w - 1.1 * h, w + 1.1 * h, Looks.WoodMid, BoxFaces.All & ~BoxFaces.Top);
        }
    }
}
