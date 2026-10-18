using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// The gompas on the kora ring, each on its OSM footprint with its front toward the stupa (boudha.md 3.5, 3.8, 3.9):
    /// <list type="bullet">
    /// <item>S5, the Guru Lhakhang (Mahaguru Gompa, "Tamang Gompa", w520755992) facing the north entrance: a portico of
    /// red columns with painted capitals, cloud-scroll brackets and a banded beam over a mural wall and a gilt-framed
    /// door; two ochre-yellow upper storeys with Tibetan windows (red frames in black trapezoids, frilled awnings); a maroon
    /// frieze set with gilt mirrors; on the roof a gilt hipped pavilion with dragon-head corners and the gilt Dharmachakra
    /// flanked by two deer; and in front, on the kora, the Big Bell in its gilt horseshoe arch (bell/09, gompa2/00-01,
    /// commons_more/c01, c04);</item>
    /// <item>S8, the Jamchen Lhakhang (Sakya Maitreya Lhakhang, w209530023), set back west of the ring: an open portico of
    /// four square maroon columns with painted capitals and brackets, a deep painted beam, white lattice screens between
    /// the columns, green marble steps and a door hung with yellow curtains; ochre side walls with maroon windows; a band
    /// of painted window panels under a dark red cornice; a gilt hipped roof (commons_late/c01, aerial);</item>
    /// <item>S9, Old Dhabzang (w209312611): a small white three-storey gompa with a maroon frieze, Tibetan windows and a
    /// gilt roof pavilion [V].</item>
    /// </list>
    /// </summary>
    internal static class BoudhaGompas
    {
        /// <summary>Tallest point of each gompa above the kora (m).</summary>
        public const double GuruTop = 15.05, JamchenTop = 12.2, DhabzangTop = 13.1;

        /// <summary>Draws the sides of a solid box (in gompa frame <paramref name="g"/>, which is in tile metres) in, 10 cm
        /// at a time, while a point of that side stands within 5 cm of a drawn road.</summary>
        private static void ClearOfRoads(TileData tile, in Affine3 g, ref double x0, ref double x1, ref double z0, ref double z1)
        {
            if (tile == null) return;
            SacredDrawnRoads roads = SacredDrawnRoads.For(tile);
            for (int it = 0; it < 60; it++)
            {
                bool l = Hits(roads, g, x0, z0, x0, z1), r = Hits(roads, g, x1, z0, x1, z1), b = Hits(roads, g, x0, z0, x1, z0), t = Hits(roads, g, x0, z1, x1, z1);
                if (!(l || r || b || t)) return;
                if (l && x1 - x0 > 1.2) x0 += 0.1;
                if (r && x1 - x0 > 1.2) x1 -= 0.1;
                if (b && z1 - z0 > 1.2) z0 += 0.1;
                if (t && z1 - z0 > 1.2) z1 -= 0.1;
            }
        }

        private static bool Hits(SacredDrawnRoads roads, in Affine3 g, double xa, double za, double xb, double zb)
        {
            double len = Math.Sqrt((xb - xa) * (xb - xa) + (zb - za) * (zb - za));
            int n = Math.Max(1, (int)Math.Ceiling(len / 0.25));
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n, x, y, z;
                g.Point(xa + (xb - xa) * t, 0, za + (zb - za) * t, out x, out y, out z);
                if (roads.SignedDistance(x, z) < 0.05) return true;
            }
            return false;
        }

        public static void Build(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            for (int i = 0; i < BoudhaRing.Houses.Length; i++)
            {
                RingHouse h = BoudhaRing.Houses[i];
                if (h.Part != part || h.Look < HouseLook.GuruLhakhang) continue;
                double[] pu, pv;
                int n = BoudhaRing.Footprint(tile, f, h.Way, out pu, out pv);
                if (n < 3) continue;
                double cu, cv, au, av, ha, hb;
                if (!BoudhaRing.OrientedRect(pu, pv, n, out cu, out cv, out au, out av, out ha, out hb)) continue;
                // The rectangle's side facing the stupa axis: its outward normal and the half sizes across and deep.
                double hx, hz, nu, nv;
                FrontOf(cu, cv, au, av, ha, hb, out nu, out nv, out hx, out hz);
                Affine3 g = BoudhaKit.Facing(f.Site, cu, 0, cv, nu, nv);
                switch (h.Look)
                {
                    case HouseLook.GuruLhakhang: Guru(m, g, hx, hz, lod); break;
                    case HouseLook.Jamchen: Jamchen(m, g, hx, hz, lod); break;
                    default: Dhabzang(m, g, hx, hz, lod); break;
                }
                if (c != null)
                {
                    // The solid sits 0.35 m inside the outline (the front steps and porch stay walkable), each side
                    // drawn further in where the oriented rectangle overhangs the drawn kora at the outline's corners.
                    double x0 = -Math.Max(1.0, hx - 0.35), x1 = -x0, z0 = -Math.Max(1.0, hz - 0.35), z1 = -z0;
                    ClearOfRoads(tile, g, ref x0, ref x1, ref z0, ref z1);
                    double x, y, z, ux, uy, uz;
                    g.Point(0.5 * (x0 + x1), 0, 0.5 * (z0 + z1), out x, out y, out z);
                    g.Vector(1, 0, 0, out ux, out uy, out uz);
                    c.AddBox(x, z, f.Level - 0.3, f.Level + 9.0, 0.5 * (x1 - x0), 0.5 * (z1 - z0), ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                }
            }
        }

        /// <summary>Of the oriented rectangle's four sides, the one whose outward normal points most toward the stupa
        /// axis: the normal and the half sizes across it (hx) and along it (hz).</summary>
        private static void FrontOf(double cu, double cv, double au, double av, double ha, double hb, out double nu, out double nv, out double hx, out double hz)
        {
            double r = Math.Max(1e-6, Math.Sqrt(cu * cu + cv * cv)), tu = -cu / r, tv = -cv / r;
            // Candidate normals: ±a (half size ha along it, hb across) and ±b (the perpendicular).
            double bu = -av, bv = au;
            double da = au * tu + av * tv, db = bu * tu + bv * tv;
            if (Math.Abs(da) >= Math.Abs(db))
            {
                double s = da >= 0 ? 1 : -1;
                nu = s * au;
                nv = s * av;
                hz = ha;
                hx = hb;
            }
            else
            {
                double s = db >= 0 ? 1 : -1;
                nu = s * bu;
                nv = s * bv;
                hz = hb;
                hx = ha;
            }
        }

        // ------------------------------------------------------------------------------------------------ Guru Lhakhang

        private static void Guru(MeshData m, in Affine3 g, double hx, double hz, int lod)
        {
            double plinth = 0.45, ground = plinth + 3.6, top = ground + 6.4;
            ShapeBrush ochre = BoudhaKit.B(BoudhaKit.Ochre, MaterialChannel.Plaster), stone = BoudhaKit.B(MeshColor.FromHex(0x9C978E), MaterialChannel.Stone);
            ShapeBrush maroon = BoudhaKit.B(BoudhaKit.Maroon, MaterialChannel.Paint), dark = BoudhaKit.B(MeshColor.FromHex(0x2A1A16), MaterialChannel.Wood);
            if (lod >= 3)
            {
                SacredDraw.Box(m, g, -hx, hx, -0.3, top, -hz, hz, ochre, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Frustum(m, SacredDraw.At(g, 0, top, -0.3 * hz) * Affine3.Yaw(45), BoudhaKit.Gilt, 3.6, 0.3, 3.0, 4, 0, 0, false, false);
                // The maroon frieze under the roof line and the dark portico across the front.
                SacredDraw.Panel(m, g, -hx, top - 0.85, hx, top, hz + 0.02, maroon);
                SacredDraw.Panel(m, g, -hx + 1.0, plinth, hx - 1.0, ground, hz + 0.02, BoudhaKit.B(MeshColor.FromHex(0x5A1E18), MaterialChannel.Paint, 0.7f));
                return;
            }
            // Body: stone plinth, the walls with a maroon frieze under a dark cornice and the parapet.
            double px = hx - 1.0;
            OpenGround(m, g, hx, hz, px, plinth, ground, stone, ochre, 0.08);
            Mould mo = SacredDraw.M;
            mo.Add(0, ground, ochre).Add(0, top - 0.85);
            if (lod <= 1) mo.Add(0.04, top - 0.83, maroon).Add(0.04, top - 0.08).Add(0.3, top, dark).Add(0.3, top + 0.12).Add(0.05, top + 0.14, ochre).Add(0.05, top + 0.5)
                            .Add(-0.15, top + 0.5).Add(-0.15, top + 0.14);
            else mo.Add(0.0, top - 0.85, maroon).Add(0, top).Add(0.25, top, dark).Add(0.25, top + 0.5);
            SacredDraw.RectRing(m, g, 0, 0, hx, hz, mo, false, ochre);
            SacredDraw.CapRect(m, g, 0, 0, hx - 0.1, hz - 0.1, top + 0.1, true, BoudhaKit.B(MeshColor.FromHex(0xB9B2A6), MaterialChannel.Concrete));
            if (lod <= 1) Mirrors(m, g, hx, hz + 0.05, top - 0.42, lod);
            // The portico on the ground storey: a painted mural wall with the door, red columns, brackets, the banded beam.
            Portico(m, g, px, hz, plinth, ground, 4, BoudhaKit.B(MeshColor.FromHex(0xB3261E), MaterialChannel.Paint), lod, false);
            // The painted balcony over the portico (bell/09): a slab on the beam, a fascia of small coloured panels and a
            // dark rail with balusters.
            if (lod <= 1) Balcony(m, g, px + 0.25, hz, ground, lod);
            // Steps along the portico, where people sit.
            SacredDraw.Box(m, g, -px, px, -0.3, 0.15, hz, hz + 0.6, stone, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, g, -px, px, -0.3, 0.3, hz, hz + 0.3, stone, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            // Upper storeys: Tibetan windows, a gilt-framed central window on the second storey.
            if (lod <= 1)
            {
                for (int s = 0; s < 2; s++)
                {
                    double y = ground + 1.4 + s * 3.1;
                    int cols = 5;
                    for (int q = 0; q < cols; q++)
                    {
                        double x = -hx + 1.2 + (2 * hx - 2.4) * q / (cols - 1);
                        if (s == 0 && q == cols / 2)
                        {
                            SacredDraw.Box(m, g, x - 1.1, x + 1.1, y - 0.95, y + 1.25, hz, hz + 0.14, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top | BoxFaces.Bottom);
                            SacredDraw.Panel(m, g, x - 0.85, y - 0.75, x + 0.85, y + 1.0, hz + 0.145, BoudhaKit.B(MeshColor.FromHex(0xB3261E), MaterialChannel.Wood));
                            BoudhaKit.GridLines(m, g, x - 0.85, x + 0.85, y - 0.75, y + 1.0, hz + 0.15, 0.28, 0.35, 0.04, BoudhaKit.Gilt);
                            continue;
                        }
                        BoudhaKit.TibetanWindow(m, g, x, y, hz, 0.95, 1.25, BoudhaKit.ClothRed, lod);
                    }
                }
            }
            else
            {
                for (int s = 0; s < 2; s++) SacredDraw.Panel(m, g, -hx + 0.8, ground + 0.8 + s * 3.1, hx - 0.8, ground + 2.0 + s * 3.1, hz + 0.02, BoudhaKit.B(MeshColor.FromHex(0x3A2420), MaterialChannel.Wood));
            }
            // The roof: the gilt pavilion over the back half, the Dharmachakra with its deer on the front parapet.
            double rz = -0.25 * hz, rw = Math.Min(3.2, hx - 1.5), rd = Math.Min(2.4, 0.55 * hz);
            SacredDraw.Box(m, g, -rw, rw, top + 0.1, top + 2.3, rz - rd, rz + rd, BoudhaKit.White, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
                for (int q = -1; q <= 1; q++) BoudhaKit.TibetanWindow(m, g, q * 1.8, top + 1.2, rz + rd, 0.7, 0.8, BoudhaKit.ClothRed, Math.Max(1, lod));
            GiltRoof(m, g, -rw - 0.6, rw + 0.6, rz - rd - 0.6, rz + rd + 0.6, top + 2.3, 1.6, lod, true);
            Banner(m, g, 0, top + 3.9, rz, 0.7, lod);
            DharmaWheel(m, g, 0, top + 0.6, hz - 0.5, 1.4, lod);
            BigBell(m, g, -2.0, hz + 2.6, lod);
        }

        /// <summary>A Tibetan balcony across a gompa front (frame g, front z = <paramref name="hz"/>) at height y, half
        /// width hx: a 0.9 m slab, its fascia of painted panels (red, green, blue between gilt strips), a dark wooden rail on
        /// turned balusters.</summary>
        private static void Balcony(MeshData m, in Affine3 g, double hx, double hz, double y, int lod)
        {
            double d = 0.9, z1 = hz + d;
            ShapeBrush slab = BoudhaKit.B(MeshColor.FromHex(0x8C3A2A), MaterialChannel.Wood), rail = BoudhaKit.B(MeshColor.FromHex(0x3A2418), MaterialChannel.Wood);
            SacredDraw.Box(m, g, -hx, hx, y - 0.05, y + 0.12, hz, z1, slab, BoxFaces.All & ~BoxFaces.Back);
            uint[] panel = { 0xB3261E, 0x2F8F4E, 0x2E5DA8 };
            int n = Math.Max(3, (int)(2 * hx / 0.9));
            for (int q = 0; q < n; q++)
            {
                double xa = -hx + 2 * hx * q / n + 0.06, xb = -hx + 2 * hx * (q + 1) / n - 0.06;
                SacredDraw.Panel(m, g, xa, y + 0.14, xb, y + 0.62, z1 - 0.04, BoudhaKit.B(MeshColor.FromHex(panel[q % 3]), MaterialChannel.Paint));
                if (lod == 0) Shapes.Cylinder(m, SacredDraw.Basis(g, 0.5 * (xa + xb), y + 0.38, z1 - 0.03, 1, 0, 0, 0, 0, 1), BoudhaKit.Gilt, 0.12, 0.02, 8, 0, 0, false, true);
            }
            SacredDraw.Box(m, g, -hx, hx, y + 0.12, y + 0.14, z1 - 0.06, z1 - 0.02, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Top);
            SacredDraw.Box(m, g, -hx, hx, y + 0.62, y + 0.66, z1 - 0.06, z1 - 0.02, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Top);
            // The rail above the fascia.
            SacredDraw.Box(m, g, -hx, hx, y + 0.98, y + 1.06, z1 - 0.1, z1, rail, BoxFaces.All & ~BoxFaces.Back);
            if (lod == 0)
                for (int q = 0; q <= 2 * n; q++)
                {
                    double x = -hx + 2 * hx * q / (2 * n);
                    Shapes.Bar(m, g, rail, x, y + 0.66, z1 - 0.05, x, y + 0.98, z1 - 0.05, 0.03, 4);
                }
        }

        /// <summary>Depth of a gompa's ground-storey portico behind its front plane (m).</summary>
        private const double PorticoDepth = 1.6;

        /// <summary>
        /// The ground storey of a gompa whose front (z = +<paramref name="hz"/>) opens into a portico between x = ±<paramref name="px"/>:
        /// the block behind the portico and the two end piers, each with its plinth (<paramref name="plinthOut"/> proud) and
        /// wall up to <paramref name="y1"/>, so the portico's recess is not walled over.
        /// </summary>
        private static void OpenGround(MeshData m, in Affine3 g, double hx, double hz, double px, double plinth, double y1, in ShapeBrush stone, in ShapeBrush wall,
                                       double plinthOut)
        {
            double zw = hz - PorticoDepth;
            Mould mo = SacredDraw.M;
            mo.Add(plinthOut, -0.3, stone).Add(plinthOut, plinth).Add(0, plinth + 0.02, wall).Add(0, y1);
            SacredDraw.RectRing(m, g, 0, 0.5 * (zw - hz), hx, 0.5 * (zw + hz), mo, false, wall);
            for (int s = -1; s <= 1; s += 2)
            {
                // The piers stop a centimetre short of the portico's side returns, which carry the mural.
                mo = SacredDraw.M;
                mo.Add(plinthOut, -0.3, stone).Add(plinthOut, plinth).Add(0, plinth + 0.02, wall).Add(0, y1);
                SacredDraw.RectRing(m, g, s * 0.5 * (hx + px + 0.01), 0.5 * (zw + hz), 0.5 * (hx - px - 0.01), 0.5 * (hz - zw), mo, false, wall);
            }
        }

        /// <summary>
        /// A Tibetan portico on the ground storey of frame <paramref name="g"/>'s front (z = +hz): the recessed painted
        /// wall (murals of the Guardian Kings in panels), a door in a gilt frame (yellow curtains on Jamchen), a row of
        /// columns with painted capitals and cloud-scroll brackets, the multi-banded beam over them, and white lattice
        /// screens between the outer columns when asked.
        /// </summary>
        private static void Portico(MeshData m, in Affine3 g, double px, double hz, double y0, double y1, int columns, in ShapeBrush col, int lod, bool screens,
                                    bool pilasters = false)
        {
            double zw = hz - PorticoDepth, beam0 = y1 - 0.85;
            ShapeBrush mural = BoudhaKit.B(MeshColor.FromHex(0x6E2A24), MaterialChannel.Paint), shade = BoudhaKit.B(MeshColor.FromHex(0x4A2220), MaterialChannel.Plaster, 0.6f);
            // The recess: back wall, ceiling, side returns, floor.
            SacredDraw.Panel(m, g, -px, y0, px, beam0, zw, mural);
            SacredDraw.Quad(m, g, -px, beam0, zw, px, beam0, zw, px, beam0, hz, -px, beam0, hz, 0, -1, 0, shade);
            for (int s = -1; s <= 1; s += 2)
                SacredDraw.Quad(m, g, s * px, y0, zw, s * px, y0, hz, s * px, beam0, hz, s * px, beam0, zw, -s, 0, 0, mural);
            SacredDraw.Quad(m, g, -px, y0, zw, px, y0, zw, px, y0, hz, -px, y0, hz, 0, 1, 0, BoudhaKit.B(MeshColor.FromHex(0x9C978E), MaterialChannel.Stone));
            // Close the walls beside the portico (the body ring stops at the front plane).
            if (lod <= 1)
            {
                // Mural panels: the four Guardian Kings either side of the door (jamchen/00, bell/09): dark painted grounds
                // (deep green, maroon, indigo, umber) each with its king in his colour, a broad robed figure under a pale
                // face, so the wall reads as murals, not as stripes of colour.
                uint[] grounds = { 0x34463C, 0x4A2C28, 0x2C3650, 0x4A3E2A }, robes = { 0x3A6A46, 0x9A3A30, 0x34508A, 0xB08A3A };
                for (int q = 0; q < 4; q++)
                {
                    double xa = q < 2 ? -px + 0.3 + q * 0.5 * (px - 1.6) : 1.3 + (q - 2) * 0.5 * (px - 1.6), xb = xa + 0.5 * (px - 1.6) - 0.2;
                    double ya = y0 + 0.6, yb = beam0 - 0.3, xm = 0.5 * (xa + xb), hw2 = 0.5 * (xb - xa);
                    SacredDraw.Panel(m, g, xa, ya, xb, yb, zw + 0.01, BoudhaKit.B(MeshColor.FromHex(grounds[q]), MaterialChannel.Paint));
                    // The king: a robe widening to the feet, his armour collar.
                    SacredDraw.Quad(m, g, xm - 0.6 * hw2, ya + 0.2, zw + 0.015, xm + 0.6 * hw2, ya + 0.2, zw + 0.015, xm + 0.3 * hw2, yb - 0.7, zw + 0.015,
                                    xm - 0.3 * hw2, yb - 0.7, zw + 0.015, 0, 0, 1, BoudhaKit.B(MeshColor.FromHex(robes[q]), MaterialChannel.Paint));
                    if (lod == 0)
                        Shapes.Ellipsoid(m, SacredDraw.At(g, xm, yb - 0.45, zw + 0.02), BoudhaKit.B(MeshColor.FromHex(0xE8C8A0), MaterialChannel.Paint), 0.2, 0.24, 0.03, 6, false);
                }
                // The door: a gilt frame, red leaves or yellow curtains, a red valance.
                SacredDraw.Box(m, g, -1.05, 1.05, y0, y0 + 2.7, zw, zw + 0.12, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                SacredDraw.Panel(m, g, -0.85, y0, 0.85, y0 + 2.5, zw + 0.125, screens ? BoudhaKit.B(BoudhaKit.ClothYellow, MaterialChannel.Fabric) : BoudhaKit.B(MeshColor.FromHex(0x9E1820), MaterialChannel.Wood));
                if (screens) SacredDraw.Panel(m, g, -0.25, y0, 0.25, y0 + 2.3, zw + 0.13, BoudhaKit.B(MeshColor.FromHex(0x1C1712), MaterialChannel.Plain, 0.5f));
                SacredDraw.Panel(m, g, -0.95, y0 + 2.3, 0.95, y0 + 2.6, zw + 0.135, BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric));
            }
            // The beam over the columns: red, a gilt lotus band, blue and green jewel bands.
            uint[] bands = { 0xB3261E, 0xE2B54A, 0x2E5DA8, 0x2F8F4E, 0xB3261E };
            double by = beam0;
            if (lod >= 2)
            {
                SacredDraw.Box(m, g, -px - 0.2, px + 0.2, beam0, y1, hz - 0.2, hz + 0.15, BoudhaKit.B(MeshColor.FromHex(bands[0]), MaterialChannel.Paint), BoxFaces.Front | BoxFaces.Bottom);
                for (int q = 0; q < columns; q++)
                {
                    double x = -px + 0.35 + (2 * px - 0.7) * q / (columns - 1);
                    SacredDraw.Box(m, g, x - 0.22, x + 0.22, y0, beam0, hz - 0.52, hz - 0.08, col, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                return;
            }
            for (int q = 0; q < bands.Length; q++)
            {
                double bh = (y1 - beam0) / bands.Length;
                SacredDraw.Box(m, g, -px - 0.2, px + 0.2, by, by + bh, hz - 0.2, hz + 0.1 + 0.03 * q, BoudhaKit.B(MeshColor.FromHex(bands[q]), MaterialChannel.Paint),
                               BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                by += bh;
            }
            // Columns with painted capitals and brackets.
            for (int q = 0; q < columns; q++)
            {
                double x = -px + 0.35 + (2 * px - 0.7) * q / (columns - 1);
                // With corner pilasters (Jamchen, jamchen/00) only the two inner columns stand free, broad and richly capped.
                if (!pilasters || (q > 0 && q < columns - 1)) Column(m, g, x, hz - 0.3, y0, beam0, col, lod, pilasters ? 0.28 : 0.2);
                if (screens && (q == 0 || q == columns - 2) && lod <= 1)
                {
                    // White steel lattice screens in the outer bays.
                    double xa = x + 0.25, xb = -px + 0.35 + (2 * px - 0.7) * (q + 1) / (columns - 1) - 0.25;
                    ShapeBrush white = BoudhaKit.B(MeshColor.FromHex(0xF0F0EC), MaterialChannel.Metal);
                    SacredDraw.Panel(m, g, xa, y0, xb, y0 + 0.12, hz - 0.3, white);
                    if (lod == 0) BoudhaKit.GridLines(m, g, xa, xb, y0, y0 + 1.25, hz - 0.3, 0.2, 0.25, 0.03, white);
                    else SacredDraw.Panel(m, g, xa, y0 + 1.15, xb, y0 + 1.25, hz - 0.3, white);
                }
            }
        }

        /// <summary>A Tibetan column: a square shaft (chamfered), a painted capital of stacked blocks and the cloud-scroll
        /// bracket (zhu) spreading under the beam.</summary>
        private static void Column(MeshData m, in Affine3 g, double x, double z, double y0, double y1, in ShapeBrush col, int lod, double w)
        {
            Mould mo = SacredDraw.M;
            mo.Add(0.05, y0, BoudhaKit.B(MeshColor.FromHex(0x9C978E), MaterialChannel.Stone)).Add(0.05, y0 + 0.2).Add(0, y0 + 0.22, col).Add(0, y1 - 0.7);
            if (lod == 0)
                mo.Add(0.03, y1 - 0.68, BoudhaKit.B(MeshColor.FromHex(0x2E5DA8), MaterialChannel.Paint)).Add(0.03, y1 - 0.55).Add(0.06, y1 - 0.53, BoudhaKit.Gilt)
                  .Add(0.06, y1 - 0.45).Add(0.03, y1 - 0.43, BoudhaKit.B(MeshColor.FromHex(0x2F8F4E), MaterialChannel.Paint)).Add(0.03, y1 - 0.3);
            else mo.Add(0.05, y1 - 0.68, BoudhaKit.B(MeshColor.FromHex(0x2E5DA8), MaterialChannel.Paint)).Add(0.05, y1 - 0.3);
            SacredDraw.RectRing(m, g, x, z, w, w, mo, false, col);
            // The bracket: a wide block that tapers down, with scroll ends.
            ShapeBrush br = BoudhaKit.B(MeshColor.FromHex(0xB3261E), MaterialChannel.Paint);
            SacredDraw.Box(m, g, x - 0.75, x + 0.75, y1 - 0.3, y1, z - 0.22, z + 0.22, br, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, g, x - 0.45, x + 0.45, y1 - 0.5, y1 - 0.3, z - 0.2, z + 0.2, br, BoxFaces.All & ~BoxFaces.Top);
            if (lod == 0)
            {
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Torus(m, SacredDraw.Basis(g, x + s * 0.7, y1 - 0.2, z + 0.23, 1, 0, 0, 0, 0, 1), BoudhaKit.Gilt, 0.1, 0.03, 8, 4);
                SacredDraw.Panel(m, g, x - 0.6, y1 - 0.27, x + 0.6, y1 - 0.03, z + 0.225, BoudhaKit.B(MeshColor.FromHex(0x2F8F4E), MaterialChannel.Paint));
            }
        }

        /// <summary>The row of gilt mirror discs (melong) set in a maroon frieze across the front.</summary>
        private static void Mirrors(MeshData m, in Affine3 g, double hx, double z, double y, int lod)
        {
            int n = Math.Max(3, (int)(2 * hx / 1.6));
            for (int q = 0; q < n; q++)
            {
                double x = -hx + (q + 0.5) * 2 * hx / n;
                // Round discs near, flat gilt squares beyond.
                if (lod == 0) Shapes.Cylinder(m, SacredDraw.Basis(g, x, y, z, 1, 0, 0, 0, 0, 1), BoudhaKit.Gilt, 0.22, 0.05, 8, 0, 0, false, true);
                else SacredDraw.Panel(m, g, x - 0.19, y - 0.19, x + 0.19, y + 0.19, z + 0.02, BoudhaKit.Gilt);
            }
        }

        /// <summary>
        /// A gilt Chinese-style hipped roof over the local rectangle x0..x1 × z0..z1 at eave height y, rising
        /// <paramref name="rise"/>: four slopes with standing-seam ribs, a ridge with a cloud-scroll cresting and, when
        /// <paramref name="dragons"/>, the gilt dragon (makara) heads curling up from the four corners (commons_more/c01).
        /// </summary>
        internal static void GiltRoof(MeshData m, in Affine3 g, double x0, double x1, double z0, double z1, double y, double rise, int lod, bool dragons)
        {
            ShapeBrush gilt = BoudhaKit.B(MeshColor.FromHex(0xD6AE48), MaterialChannel.Gilt), giltLit = BoudhaKit.Gilt;
            double cx = 0.5 * (x0 + x1), cz = 0.5 * (z0 + z1), a = 0.5 * (x1 - x0), b = 0.5 * (z1 - z0);
            bool alongX = a >= b;
            double rh = alongX ? a - b : b - a;
            Affine3 rf = alongX ? SacredDraw.At(g, cx, y, cz) : SacredDraw.Turn(g, cx, y, cz, 0.5 * Math.PI);
            double A = alongX ? a : b, B = alongX ? b : a;
            // Slightly concave slopes: an upper and a lower facet per side (the eaves kick up a little).
            double kink = 0.35, ky = rise * 0.22, kb = B * (1 - kink), ka = A - B * kink;
            for (int s = -1; s <= 1; s += 2)
            {
                // Long sides: lower (eave → kink) and upper (kink → ridge) facets.
                Facet(m, rf, -A, 0, s * B, A, 0, s * B, ka, ky, s * kb, -ka, ky, s * kb, gilt);
                Facet(m, rf, -ka, ky, s * kb, ka, ky, s * kb, rh, rise, 0, -rh, rise, 0, gilt);
                // Hip ends.
                FacetTri(m, rf, s * A, 0, -B, s * A, 0, B, s * ka, ky, kb, s * ka, ky, -kb, gilt);
                FacetTri2(m, rf, s * ka, ky, -kb, s * ka, ky, kb, s * rh, rise, 0, gilt);
            }
            SacredDraw.Quad(m, rf, -A, -0.03, -B, A, -0.03, -B, A, -0.03, B, -A, -0.03, B, 0, -1, 0, BoudhaKit.B(MeshColor.FromHex(0x8E2A22), MaterialChannel.Wood, 0.7f));
            if (lod >= 2) return;
            // Ridge with cresting, and the corner dragons.
            if (rh > 0.05) Shapes.Bar(m, rf, giltLit, -rh, rise + 0.05, 0, rh, rise + 0.05, 0, 0.1, 6);
            if (lod == 0)
            {
                int crest = Math.Max(1, (int)(2 * rh / 0.45));
                for (int q = 0; q <= crest; q++)
                {
                    double x = -rh + 2 * rh * q / Math.Max(1, crest);
                    Shapes.Ellipsoid(m, SacredDraw.At(rf, x, rise + 0.2, 0), giltLit, 0.14, 0.16, 0.05, 6, false);
                }
            }
            if (!dragons) return;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2) Dragon(m, rf, sx * A, sz * B, sx, sz, lod);
        }

        private static void Facet(MeshData m, in Affine3 f, double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2, double x3,
                                  double y3, double z3, in ShapeBrush b)
        {
            // Normal from the quad's diagonals, pointed up.
            double ax = x2 - x0, ay = y2 - y0, az = z2 - z0, bx = x3 - x1, by = y3 - y1, bz = z3 - z1;
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx, l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-9) return;
            if (ny < 0)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            SacredDraw.Quad(m, f, x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3, nx / l, ny / l, nz / l, b);
        }

        private static void FacetTri(MeshData m, in Affine3 f, double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2,
                                     double x3, double y3, double z3, in ShapeBrush b)
        {
            Facet(m, f, x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3, b);
        }

        private static void FacetTri2(MeshData m, in Affine3 f, double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2, in ShapeBrush b)
        {
            double ax = x1 - x0, ay = y1 - y0, az = z1 - z0, bx = x2 - x0, by = y2 - y0, bz = z2 - z0;
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx, l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-9) return;
            if (ny < 0)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            SacredDraw.Tri(m, f, x0, y0, z0, x1, y1, z1, x2, y2, z2, nx / l, ny / l, nz / l, b);
        }

        /// <summary>A gilt dragon (makara) head curling up and out from a roof corner (commons_more/c01): a scaly neck, the
        /// head with its upturned trunk-snout and a white tusk, horns behind.</summary>
        private static void Dragon(MeshData m, in Affine3 rf, double x, double z, int sx, int sz, int lod)
        {
            ShapeBrush gilt = BoudhaKit.Gilt, white = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Paint);
            double ox = sx * 0.707, oz = sz * 0.707;
            if (lod >= 1)
            {
                Shapes.Bar(m, rf, gilt, x - ox * 0.4, 0.1, z - oz * 0.4, x + ox * 0.35, 0.55, z + oz * 0.35, 0.12, 5);
                return;
            }
            // Neck along the hip, the head at the corner, the snout curling up.
            Shapes.Bar(m, rf, gilt, x - ox * 0.7, 0.25, z - oz * 0.7, x + ox * 0.1, 0.35, z + oz * 0.1, 0.13, 6);
            Shapes.Ellipsoid(m, SacredDraw.At(rf, x + ox * 0.2, 0.42, z + oz * 0.2), gilt, 0.2, 0.18, 0.2, 8, false);
            Shapes.Bar(m, rf, gilt, x + ox * 0.35, 0.45, z + oz * 0.35, x + ox * 0.5, 0.8, z + oz * 0.5, 0.06, 5);
            Shapes.Bar(m, rf, white, x + ox * 0.3, 0.32, z + oz * 0.3, x + ox * 0.45, 0.25, z + oz * 0.45, 0.025, 4);
            Shapes.Bar(m, rf, white, x + ox * 0.05, 0.55, z + oz * 0.05, x - ox * 0.2, 0.75, z - oz * 0.2, 0.025, 4);
        }

        /// <summary>A gilt victory banner (gyaltsen): a fluted cylinder on a lotus foot under a small parasol and a
        /// jewel, <paramref name="h"/> tall, standing at (x, y, z) of frame g.</summary>
        internal static void Banner(MeshData m, in Affine3 g, double x, double y, double z, double h, int lod)
        {
            Profile2 p = BoudhaKit.Prof;
            double r = 0.2 * h;
            if (lod >= 2) p.Add(0, 0).Add(0.8 * r, 0, true).Add(0.65 * r, 0.62 * h, true).Add(1.0 * r, 0.7 * h).Add(0, h);
            else p.Add(0, 0).Add(0.9 * r, 0, true).Add(0.75 * r, 0.1 * h).Add(0.65 * r, 0.15 * h, true).Add(0.65 * r, 0.62 * h, true).Add(1.0 * r, 0.7 * h).Add(0.55 * r, 0.78 * h)
                  .Add(0.3 * r, 0.85 * h).Add(0.35 * r, 0.92 * h).Add(0, h);
            Shapes.Lathe(m, SacredDraw.At(g, x, y, z), BoudhaKit.Gilt, p, lod == 0 ? 8 : lod == 1 ? 6 : 4);
        }

        /// <summary>The gilt Dharmachakra on its lotus pedestal flanked by two kneeling deer (commons_more/c04), the wheel
        /// <paramref name="d"/> across, standing on the parapet at (x, y, z) of frame g and facing +z.</summary>
        internal static void DharmaWheel(MeshData m, in Affine3 g, double x, double y, double z, double d, int lod)
        {
            ShapeBrush gilt = BoudhaKit.Gilt, giltDeep = BoudhaKit.B(MeshColor.FromHex(0xC99A38), MaterialChannel.Gilt);
            double r = 0.5 * d;
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, 0).Add(0.45 * r, 0, true).Add(0.5 * r, 0.12 * r).Add(0.32 * r, 0.25 * r).Add(0.22 * r, 0.35 * r, true).Add(0, 0.35 * r);
            if (lod <= 1) Shapes.Lathe(m, SacredDraw.At(g, x, y, z), giltDeep, p, lod == 0 ? 12 : 6);
            double cy = y + 0.35 * r + r;
            Affine3 wf = SacredDraw.Basis(g, x, cy, z, 1, 0, 0, 0, 0, 1);
            // In wf the wheel's plane is local x-z (facing local y = g's +z).
            int seg = lod == 0 ? 18 : lod == 1 ? 10 : 6;
            Shapes.Torus(m, SacredDraw.Basis(g, x, cy, z, 1, 0, 0, 0, 0, 1) * Affine3.RotationX(0), gilt, 0.86 * r, 0.12 * r, seg, lod == 0 ? 5 : 3);
            if (lod <= 1)
            {
                Shapes.Cylinder(m, SacredDraw.At(wf, 0, -0.08 * r, 0), giltDeep, 0.22 * r, 0.16 * r, lod == 0 ? 10 : 6, 0, 0, true, true);
                for (int q = 0; q < 8; q++)
                {
                    double a = Math.PI * q / 4;
                    Shapes.Bar(m, wf, gilt, 0.2 * r * Math.Cos(a), 0, 0.2 * r * Math.Sin(a), 0.8 * r * Math.Cos(a), 0, 0.8 * r * Math.Sin(a), 0.05 * r, lod == 0 ? 4 : 3);
                }
                // Flame jewel on top.
                Shapes.Ellipsoid(m, SacredDraw.At(g, x, cy + r + 0.12 * r, z), gilt, 0.12 * r, 0.2 * r, 0.08 * r, 6, false);
            }
            // The two deer kneeling either side, facing the wheel (near only: they read as gilt lumps beyond).
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2) Deer(m, g, x + s * (r + 0.55), y, z, -s, 0.55 * d, lod);
        }

        /// <summary>A gilt deer kneeling on its folded legs, its head raised toward the wheel (direction sx along x).</summary>
        private static void Deer(MeshData m, in Affine3 g, double x, double y, double z, int sx, double h, int lod)
        {
            ShapeBrush gilt = BoudhaKit.Gilt;
            int seg = lod == 0 ? 8 : 5;
            Shapes.Ellipsoid(m, SacredDraw.At(g, x, y + 0.3 * h, z), gilt, 0.42 * h, 0.26 * h, 0.22 * h, seg, false);
            if (lod >= 2) return;
            Shapes.Bar(m, g, gilt, x + sx * 0.28 * h, y + 0.38 * h, z, x + sx * 0.42 * h, y + 0.78 * h, z, 0.09 * h, 5);
            Shapes.Ellipsoid(m, SacredDraw.At(g, x + sx * 0.5 * h, y + 0.86 * h, z), gilt, 0.16 * h, 0.12 * h, 0.1 * h, seg, false);
            Shapes.Bar(m, g, gilt, x + sx * 0.58 * h, y + 0.82 * h, z, x + sx * 0.72 * h, y + 0.78 * h, z, 0.05 * h, 4);
            if (lod == 0)
            {
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Bar(m, g, gilt, x + sx * 0.46 * h, y + 0.94 * h, z + s * 0.06 * h, x + sx * 0.4 * h, y + 1.08 * h, z + s * 0.14 * h, 0.025 * h, 3);
                Shapes.Bar(m, g, gilt, x - sx * 0.3 * h, y + 0.1 * h, z + 0.15 * h, x + sx * 0.2 * h, y + 0.08 * h, z + 0.2 * h, 0.06 * h, 4);
            }
        }

        private const double BellLip = 1.35;

        /// <summary>The Big Bell on the kora in front of the Guru Lhakhang (bell/00-09, gompa2/01): a silver-bronze bell
        /// hanging in a gilt horseshoe-arch frame on a low stone base, at (x, z) of frame g.</summary>
        private static void BigBell(MeshData m, in Affine3 g, double x, double z, int lod)
        {
            ShapeBrush gilt = BoudhaKit.Gilt, bronze = BoudhaKit.B(MeshColor.FromHex(0xA9A39A), MaterialChannel.Metal), stone = BoudhaKit.B(MeshColor.FromHex(0x8E8A82), MaterialChannel.Stone);
            // Proportions from bell/04 and bell/08 (by the post span): the posts stand 1.75 over the base, the horseshoe
            // springs just above them, and the bell (1.25 across the lip) hangs from the crown of the arch with its lip at
            // 1.1 over the base, its own crown close under the arch.
            double y0 = BoudhaQuarter.PaveTop, span = 1.15, ph = 2.0;
            Affine3 b = SacredDraw.At(g, x, y0, z);
            SacredDraw.Box(m, b, -span - 0.35, span + 0.35, -0.05, 0.25, -0.45, 0.45, stone, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod >= 2)
            {
                // Far: the two posts, a flat gilt arch board over them and the bell.
                for (int s = -1; s <= 1; s += 2)
                    SacredDraw.Box(m, b, s * span - 0.16, s * span + 0.16, 0.25, ph + 0.6, -0.16, 0.16, gilt, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, b, -span - 0.2, span + 0.2, ph + 0.6, ph + 1.3, -0.15, 0.15, gilt, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
                Profile2 fp = BoudhaKit.Prof;
                fp.Add(0, BellLip).Add(0.62, BellLip, true).Add(0.36, BellLip + 1.1).Add(0, BellLip + 1.3);
                Shapes.Lathe(m, b, bronze, fp, 5);
                return;
            }
            int seg = lod == 0 ? 10 : 6;
            for (int s = -1; s <= 1; s += 2)
            {
                Mould mo = SacredDraw.M;
                if (lod >= 1) mo.Add(0.04, 0.25, gilt).Add(0.04, ph);
                else mo.Add(0.08, 0.25, gilt).Add(0.08, 0.6).Add(0.0, 0.65).Add(0.0, ph - 0.25).Add(0.08, ph - 0.2).Add(0.08, ph);
                SacredDraw.RectRing(m, b, s * span, 0, 0.16, 0.16, mo, true, gilt);
            }
            // The horseshoe arch over the pillars (bell/09): a flat gilt frame (a partial torus squashed in depth, 250° of
            // sweep with the gap at the bottom, angle 0 pointing down in this frame), an inner bead, and a flame jewel crest.
            Affine3 arch = SacredDraw.Basis(b, 0, ph + 0.35, 0, 1, 0, 0, 0, 0, 1);
            Shapes.Torus(m, arch * Affine3.Scaling(1, 0.55, 1), gilt, span + 0.05, 0.24, lod == 0 ? 22 : lod == 1 ? 10 : 7, lod == 0 ? 6 : 4, default, 55, 250, lod <= 1);
            if (lod == 0)
            {
                Shapes.Torus(m, arch, BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt), span - 0.22, 0.05, 20, 4, default, 60, 240, true);
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, ph + 0.35 + span + 0.38, 0), gilt, 0.2, 0.32, 0.1, 8, false);
                Shapes.Cone(m, SacredDraw.At(b, 0, ph + 0.35 + span + 0.6, 0), gilt, 0.08, 0.3, 6, 0, 0, false);
            }
            // The bell, hung from the crown of the arch by a short gilt shank (no crossbar: bell/04, bell/08).
            double l = BellLip, crown = l + 1.3, archIn = ph + 0.35 + span + 0.05 - 0.24;
            Profile2 p = BoudhaKit.Prof;
            if (lod >= 1) p.Add(0, l).Add(0.62, l, true).Add(0.52, l + 0.3).Add(0.38, l + 1.05).Add(0.08, crown).Add(0, crown);
            else p.Add(0, l).Add(0.62, l, true).Add(0.6, l + 0.08).Add(0.52, l + 0.3).Add(0.44, l + 0.7).Add(0.38, l + 1.05).Add(0.3, l + 1.22).Add(0.08, crown).Add(0, crown);
            if (lod == 0) p.Smooth(1);
            Shapes.Lathe(m, b, bronze, p, lod == 0 ? 16 : lod == 1 ? 8 : 6);
            Shapes.Bar(m, b, gilt, 0, crown - 0.02, 0, 0, archIn + 0.05, 0, lod == 0 ? 0.06 : 0.05, lod == 0 ? 5 : 4);
            if (lod == 0) Shapes.Torus(m, SacredDraw.At(b, 0, l + 0.25, 0), bronze, 0.555, 0.03, seg + 6, 4);
        }

        // ------------------------------------------------------------------------------------------------ Jamchen

        private static void Jamchen(MeshData m, in Affine3 g, double hx, double hz, int lod)
        {
            // Section (jamchen/00, commons Jamchen Lhakhang): a marble plinth, a tall portico (4.6 m to the beam top) under a
            // deep dark eave, the upper storey set back behind a gallery of painted lattice panels, a second eave and the
            // gilt roof.
            double plinth = 0.6, ground = plinth + 4.6, eaveTop = ground + 0.3, top = eaveTop + 3.0, back = 0.45;
            ShapeBrush ochre = BoudhaKit.B(BoudhaKit.OchreDeep, MaterialChannel.Plaster), marble = BoudhaKit.B(MeshColor.FromHex(0xE6E2DA), MaterialChannel.Stone);
            ShapeBrush redCornice = BoudhaKit.B(MeshColor.FromHex(0x7A1F1F), MaterialChannel.Paint), maroon = BoudhaKit.B(BoudhaKit.MonkMaroon, MaterialChannel.Paint);
            ShapeBrush navy = BoudhaKit.B(MeshColor.FromHex(0x1F2C46), MaterialChannel.Paint, 0.7f), slate = BoudhaKit.B(MeshColor.FromHex(0x343A44), MaterialChannel.Metal);
            ShapeBrush red = BoudhaKit.B(MeshColor.FromHex(0xA3221C), MaterialChannel.Paint);
            if (lod >= 3)
            {
                SacredDraw.Box(m, g, -hx, hx, -0.3, top, -hz, hz, ochre, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Frustum(m, SacredDraw.At(g, 0, top, 0) * Affine3.Yaw(45), BoudhaKit.Gilt, 1.41 * (Math.Max(hx, hz) + 0.6), 0.3, 2.6, 4, 0, 0, false, false);
                return;
            }
            double px = hx - 0.9;
            OpenGround(m, g, hx, hz, px, plinth, ground, marble, ochre, 0.06);
            Portico(m, g, px, hz, plinth, ground, 4, maroon, lod, true, true);
            // Three green marble steps in front of the door.
            ShapeBrush green = BoudhaKit.B(MeshColor.FromHex(0x4E6B5A), MaterialChannel.Stone);
            for (int s = 0; s < 3; s++)
                SacredDraw.Box(m, g, -1.6, 1.6, -0.3, plinth * (3 - s) / 3.0, hz, hz + 0.35 * (s + 1), green, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            double beam0 = ground - 0.85;
            if (lod <= 1)
            {
                // Maroon pilasters on the portico's corners, and the red valance with gilt cusps hanging in the bays
                // between the column brackets.
                for (int s = -1; s <= 1; s += 2)
                    SacredDraw.Box(m, g, s > 0 ? px : -px - 0.35, s > 0 ? px + 0.35 : -px, plinth, beam0, hz - 0.05, hz + 0.08, maroon, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                for (int q = 0; q < 3; q++)
                {
                    // The valance hangs across the whole bay in front of the capitals (jamchen/00).
                    double xa = -px + 0.35 + (2 * px - 0.7) * q / 3.0 + 0.3, xb = -px + 0.35 + (2 * px - 0.7) * (q + 1) / 3.0 - 0.3;
                    if (xb - xa < 0.6) continue;
                    double vy = beam0 - 0.75, z = hz - 0.05;
                    SacredDraw.Panel(m, g, xa, vy, xb, beam0, z, red);
                    int cusps = Math.Max(2, (int)((xb - xa) / 0.55));
                    double cw = (xb - xa) / cusps;
                    for (int c = 0; c < cusps; c++)
                    {
                        double cx = xa + (c + 0.5) * cw;
                        if (lod == 0)
                        {
                            // A hanging gilt cusp (a half ring under the panel's edge) and a gilt medallion on the panel.
                            Shapes.Torus(m, SacredDraw.Basis(g, cx, vy, z + 0.01, 1, 0, 0, 0, 0, 1), BoudhaKit.Gilt, 0.5 * cw - 0.03, 0.035, 8, 4, default, 270, 180, false);
                            if (c % 2 == 0) Shapes.Cylinder(m, SacredDraw.Basis(g, cx, beam0 - 0.38, z, 1, 0, 0, 0, 0, 1), BoudhaKit.Gilt, 0.2, 0.04, 10, 0, 0, false, true);
                        }
                        else
                        {
                            SacredDraw.Tri(m, g, cx - 0.5 * cw + 0.03, vy, z + 0.01, cx + 0.5 * cw - 0.03, vy, z + 0.01, cx, vy - 0.2, z + 0.01, 0, 0, 1, BoudhaKit.Gilt);
                        }
                    }
                }
                // A row of painted block ends (blue, red, green) under the eave.
                if (lod == 0)
                {
                    uint[] ends = { 0x2E5DA8, 0xB3261E, 0x2F8F4E };
                    int nEnds = (int)((2 * px + 0.4) / 0.3);
                    for (int q = 0; q < nEnds; q++)
                    {
                        double x = -px - 0.2 + (q + 0.5) * (2 * px + 0.4) / nEnds;
                        SacredDraw.Box(m, g, x - 0.07, x + 0.07, ground - 0.32, ground - 0.18, hz + 0.1, hz + 0.24, BoudhaKit.B(MeshColor.FromHex(ends[q % 3]), MaterialChannel.Paint),
                                       BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
                    }
                }
            }
            // The deep portico eave (jamchen/00): a dark slab sloping steeply out 1.3 m past the front from just under the
            // gallery rail, its navy soffit rising back to the beam (seen from the kora as a broad dark band) and a gilt edge.
            double ez0 = hz - 0.3, ez1 = hz + 1.3, ey0 = ground, ey1 = eaveTop + 0.2, lip = ground, sof = ground - 0.45;
            double tn = Math.Sqrt((ez1 - ez0) * (ez1 - ez0) + (ey1 - lip) * (ey1 - lip)), sn = Math.Sqrt((ez1 - ez0) * (ez1 - ez0) + (ey0 - sof) * (ey0 - sof));
            SacredDraw.Quad(m, g, -hx - 0.35, ey1, ez0, hx + 0.35, ey1, ez0, hx + 0.35, lip, ez1, -hx - 0.35, lip, ez1, 0, (ez1 - ez0) / tn, (ey1 - lip) / tn, slate);
            SacredDraw.Quad(m, g, -hx - 0.35, sof, ez1, hx + 0.35, sof, ez1, hx + 0.35, ey0, ez0, -hx - 0.35, ey0, ez0, 0, -(ez1 - ez0) / sn, (ey0 - sof) / sn, navy);
            SacredDraw.Quad(m, g, -hx - 0.35, sof, ez1, -hx - 0.35, lip, ez1, hx + 0.35, lip, ez1, hx + 0.35, sof, ez1, 0, 0, 1, slate);
            SacredDraw.Panel(m, g, -hx - 0.35, sof, hx + 0.35, sof + (lod == 0 ? 0.08 : 0.1), ez1 + 0.01, BoudhaKit.Gilt);
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * (hx + 0.35);
                SacredDraw.Quad(m, g, x, ey0, ez0, x, ey1, ez0, x, lip, ez1, x, sof, ez1, s, 0, 0, slate);
            }
            // Upper storey, set back behind the gallery: ochre walls under a red cornice.
            Mould mo = SacredDraw.M;
            mo.Add(0, ey0, ochre).Add(0, top - 0.25).Add(0.6, top - 0.2, redCornice).Add(0.6, top + 0.15);
            SacredDraw.RectRing(m, g, 0, -0.5 * back, hx, hz - 0.5 * back, mo, false, ochre);
            double fz = hz - back;
            if (lod <= 1)
            {
                // The gallery: a red frame with painted lattice panels (white with blue tracery) over a maroon rail.
                double g0 = eaveTop + 0.15, g1 = top - 0.45;
                SacredDraw.Box(m, g, -hx + 0.25, hx - 0.25, g0, g0 + 0.35, fz, fz + 0.18, maroon, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, g, -hx + 0.25, hx - 0.25, g1, g1 + 0.2, fz, fz + 0.18, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                int panels = Math.Max(5, (int)((2 * hx - 0.5) / 0.95));
                ShapeBrush lattice = BoudhaKit.B(MeshColor.FromHex(0xEDEFF2), MaterialChannel.Paint), tracery = BoudhaKit.B(MeshColor.FromHex(0x3F6FA8), MaterialChannel.Paint);
                for (int q = 0; q < panels; q++)
                {
                    double x0 = -hx + 0.25 + (2 * hx - 0.5) * q / panels, x1 = x0 + (2 * hx - 0.5) / panels;
                    SacredDraw.Box(m, g, x0, x0 + 0.1, g0 + 0.35, g1, fz, fz + 0.16, red, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    SacredDraw.Panel(m, g, x0 + 0.1, g0 + 0.35, x1, g1, fz + 0.08, lattice);
                    if (lod == 0) BoudhaKit.GridLines(m, g, x0 + 0.18, x1 - 0.08, g0 + 0.45, g1 - 0.1, fz + 0.09, 0.22, 0.3, 0.025, tracery);
                }
                SacredDraw.Box(m, g, hx - 0.35, hx - 0.25, g0 + 0.35, g1, fz, fz + 0.16, red, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                for (int s = -1; s <= 1; s += 2)
                {
                    Affine3 side = SacredDraw.Turn(g, s * hx, 0, 0, s * 0.5 * Math.PI);
                    for (int q = -1; q <= 1; q++)
                    {
                        BoudhaKit.TibetanWindow(m, side, q * 0.6 * hz, eaveTop + 1.2, 0, 0.9, 1.2, BoudhaKit.Maroon, lod);
                        BoudhaKit.TibetanWindow(m, side, q * 0.6 * hz, plinth + 1.8, 0, 0.9, 1.4, BoudhaKit.Maroon, lod);
                    }
                }
            }
            else
            {
                SacredDraw.Panel(m, g, -hx + 0.25, eaveTop + 0.5, hx - 0.25, top - 0.45, fz + 0.02, BoudhaKit.B(MeshColor.FromHex(0xC9CED6), MaterialChannel.Paint));
            }
            GiltRoof(m, g, -hx - 0.6, hx + 0.6, -hz - 0.6, hz + 0.6, top + 0.15, 2.2, lod, true);
            Banner(m, g, 0, top + 2.4, 0, 1.0, lod);
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2) Banner(m, g, s * (hx - 0.3), top + 0.15, hz + 0.3, 0.9, lod);
        }

        // ------------------------------------------------------------------------------------------------ Dhabzang

        private static void Dhabzang(MeshData m, in Affine3 g, double hx, double hz, int lod)
        {
            double plinth = 0.3, top = plinth + 9.3;
            ShapeBrush white = BoudhaKit.White, maroon = BoudhaKit.B(BoudhaKit.Maroon, MaterialChannel.Paint), dark = BoudhaKit.B(MeshColor.FromHex(0x2A1A16), MaterialChannel.Wood);
            if (lod >= 3)
            {
                SacredDraw.Box(m, g, -hx, hx, -0.3, top, -hz, hz, white, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Frustum(m, SacredDraw.At(g, 0, top, 0) * Affine3.Yaw(45), BoudhaKit.Gilt, 2.6, 0.3, 2.0, 4, 0, 0, false, false);
                SacredDraw.Panel(m, g, -hx, top - 0.8, hx, top, hz + 0.02, maroon);
                return;
            }
            Mould mo = SacredDraw.M;
            mo.Add(0.06, -0.3, BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster)).Add(0.06, plinth).Add(0, plinth + 0.02, white).Add(0, top - 0.8, white)
              .Add(0.04, top - 0.78, maroon).Add(0.04, top - 0.06).Add(0.28, top, dark).Add(0.28, top + 0.12).Add(0.05, top + 0.14, white).Add(0.05, top + 0.45)
              .Add(-0.12, top + 0.45).Add(-0.12, top + 0.14);
            SacredDraw.RectRing(m, g, 0, 0, hx, hz, mo, false, white);
            SacredDraw.CapRect(m, g, 0, 0, hx - 0.1, hz - 0.1, top + 0.1, true, BoudhaKit.B(MeshColor.FromHex(0xB9B2A6), MaterialChannel.Concrete));
            if (lod <= 1)
            {
                Mirrors(m, g, hx, hz + 0.05, top - 0.4, lod);
                for (int s = 0; s < 3; s++)
                    for (int q = -1; q <= 1; q++)
                    {
                        if (s == 0 && q == 0)
                        {
                            SacredDraw.Box(m, g, -0.75, 0.75, plinth, plinth + 2.5, hz, hz + 0.1, BoudhaKit.Gilt, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                            SacredDraw.Panel(m, g, -0.6, plinth, 0.6, plinth + 2.35, hz + 0.105, BoudhaKit.B(MeshColor.FromHex(0x9E1820), MaterialChannel.Wood));
                            continue;
                        }
                        BoudhaKit.TibetanWindow(m, g, q * 0.62 * hx, plinth + 1.5 + s * 3.0, hz, 0.85, 1.15, BoudhaKit.ClothRed, lod);
                    }
            }
            double rw = Math.Min(1.7, 0.5 * hx), rd = Math.Min(1.5, 0.45 * hz);
            SacredDraw.Box(m, g, -rw, rw, top + 0.1, top + 1.8, -rd, rd, white, BoxFaces.All & ~BoxFaces.Bottom);
            GiltRoof(m, g, -rw - 0.5, rw + 0.5, -rd - 0.5, rd + 0.5, top + 1.8, 1.2, lod, lod == 0);
            Banner(m, g, 0, top + 3.0, 0, 0.5, lod);
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2) Banner(m, g, s * (hx - 0.3), top + 0.45, hz - 0.3, 0.8, lod);
        }
    }
}
