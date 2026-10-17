using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The palace kit of the Hanuman Dhoka site (docs/research/heritage/kds-palace.md): the brief's colours as material
    /// looks and the facade, window, gallery, roof and statue elements the structures are built from. Every element
    /// takes a face frame (u across the wall, v up, w out; the wall plane at w = 0) or a plan frame, and its LOD:
    /// LOD0 the carved detail, LOD1 the frames and masses (about a third), LOD2 dark window bands and plain masses (the
    /// silhouette and the colours), LOD3 is drawn by each structure as a few boxes. Never uses the sanctum colour
    /// (<see cref="SacredPalette.SanctumDark"/>): the palace's doorways are open passages, drawn in their own dark.
    /// </summary>
    internal static class Kp
    {
        private static ShapeBrush B(uint rgb, MaterialChannel ch, float ao = 1f)
        {
            return new ShapeBrush(MeshColor.FromHex(rgb), ch, ao);
        }

        // --- Rana white ranges (§3.3, §3.7) ---------------------------------------------------------------------------
        public static readonly ShapeBrush White = B(0xF2F0EA, MaterialChannel.Plaster);
        public static readonly ShapeBrush WhiteTrim = B(0xFAF8F2, MaterialChannel.Plaster);
        public static readonly ShapeBrush WhiteRelief = B(0xE3DFD5, MaterialChannel.Plaster);
        public static readonly ShapeBrush Dado = B(0xA8322A, MaterialChannel.Paint);
        public static readonly ShapeBrush GalleryWood = B(0x2B211B, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush GalleryBeam = B(0x241B16, MaterialChannel.Wood);
        public static readonly ShapeBrush Shutter = B(0x3A2C24, MaterialChannel.Wood);
        public static readonly ShapeBrush Green = B(0x1F7A55, MaterialChannel.Paint);
        public static readonly ShapeBrush Cream = B(0xE9D9A6, MaterialChannel.Plaster);
        public static readonly ShapeBrush Glass = B(0x2C3438, MaterialChannel.Glass);

        // --- Newar brick ranges and towers (§3.4, §3.11-3.16, §3.19) ----------------------------------------------------
        public static readonly ShapeBrush Brick = B(0x9C4A36, MaterialChannel.Brick);
        public static readonly ShapeBrush BrickNew = B(0xB5583E, MaterialChannel.Brick);
        public static readonly ShapeBrush BrickGlazed = B(0x9A4532, MaterialChannel.BrickGlazed);
        public static readonly ShapeBrush BrickKumari = B(0xA6563F, MaterialChannel.Brick);
        public static readonly ShapeBrush Wood = B(0x3B2A20, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodMid = B(0x5A3E2C, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodBeam = B(0x33251C, MaterialChannel.Wood);
        public static readonly ShapeBrush RedPost = B(0x9E3B2B, MaterialChannel.Paint);

        /// <summary>The dark of an open window or passage (not the sanctum colour).</summary>
        public static readonly ShapeBrush Recess = B(0x1C1511, MaterialChannel.Plain, 0.6f);

        // --- Roofs ----------------------------------------------------------------------------------------------------
        public static readonly ShapeBrush Tile = B(0x6E4B3A, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileRidge = B(0x553829, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileMoss = B(0x6B6047, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TowerTile = B(0x5A3E30, MaterialChannel.RoofTile);
        public static readonly ShapeBrush Copper = B(0x7F9686, MaterialChannel.Metal);
        public static readonly ShapeBrush CopperSeam = B(0x5E7466, MaterialChannel.Metal);
        public static readonly ShapeBrush CopperDark = B(0x5A5446, MaterialChannel.Metal);
        public static readonly ShapeBrush DeguCopper = B(0x5C5A4C, MaterialChannel.Metal);
        public static readonly ShapeBrush KirtipurCopper = B(0x6F8A7E, MaterialChannel.Metal);
        public static readonly ShapeBrush KirtipurRib = B(0x4E6258, MaterialChannel.Metal);
        public static readonly ShapeBrush RoundRoof = B(0x5A4537, MaterialChannel.RoofTile);
        public static readonly ShapeBrush Valance = B(0xB8262B, MaterialChannel.Fabric);
        public static readonly ShapeBrush ValanceWhite = B(0xF2EEE6, MaterialChannel.Fabric);

        // --- Metal, paint, stone, ground --------------------------------------------------------------------------------
        public static readonly ShapeBrush Gilt = B(0xD4A93A, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltDeep = B(0xB8902E, MaterialChannel.Gilt);
        public static readonly ShapeBrush GateBlue = B(0x2E6FA3, MaterialChannel.Paint);
        public static readonly ShapeBrush GateBlueDeep = B(0x1F4E78, MaterialChannel.Paint);
        public static readonly ShapeBrush Iron = B(0x25262A, MaterialChannel.Metal);
        public static readonly ShapeBrush Flags = B(0x8E8A84, MaterialChannel.Flagstone);
        public static readonly ShapeBrush ForecourtFlags = B(0x8F8B83, MaterialChannel.Flagstone);
        public static readonly ShapeBrush Step = B(0x9A958C, MaterialChannel.Stone);
        public static readonly ShapeBrush StoneGrey = B(0x8A867E, MaterialChannel.Stone);
        public static readonly ShapeBrush Carved = B(0x8C877D, MaterialChannel.Plain);
        public static readonly ShapeBrush BlackStone = B(0x2E2D2B, MaterialChannel.Plain);
        public static readonly ShapeBrush Silver = B(0xC9CED6, MaterialChannel.Metal);
        public static readonly ShapeBrush RedOchre = B(0xC4472E, MaterialChannel.Plaster);
        public static readonly ShapeBrush Sindoor = B(0x8E2B22, MaterialChannel.Paint);
        public static readonly ShapeBrush Cloak = B(0xC8102E, MaterialChannel.Fabric);
        public static readonly ShapeBrush Parasol = B(0x2A2422, MaterialChannel.Fabric);
        public static readonly ShapeBrush Grass = B(0x6F7A45, MaterialChannel.Grass);
        public static readonly ShapeBrush BrickPave = B(0xA0503A, MaterialChannel.Brick);
        public static readonly ShapeBrush GaddiWhite = B(0xF4F2EC, MaterialChannel.Plaster);
        public static readonly ShapeBrush GaddiShade = B(0xE2DFD7, MaterialChannel.Plaster);
        public static readonly ShapeBrush Teal = B(0x2E6F86, MaterialChannel.Paint);
        public static readonly ShapeBrush GaddiRoof = B(0x7E3A34, MaterialChannel.RoofTile);
        public static readonly ShapeBrush Pot = B(0xA4563A, MaterialChannel.Dirt);
        public static readonly ShapeBrush Leaves = B(0x3F7A3A, MaterialChannel.Foliage);
        public static readonly ShapeBrush Flowers = B(0xD8452E, MaterialChannel.Foliage);

        public static ShapeBrush Ao(in ShapeBrush b, float ao)
        {
            return new ShapeBrush(b.Color, b.Channel, ao);
        }

        public static ShapeBrush Tint(in ShapeBrush b, float f)
        {
            return new ShapeBrush(MeshColor.Scale(b.Color, f), b.Channel, b.Ao);
        }

        // =================================================================================================================
        // Masses
        // =================================================================================================================

        /// <summary>A box in frame coordinates (see <see cref="SacredDraw.Box"/>).</summary>
        public static void Box(MeshData m, in Affine3 k, double u0, double u1, double v0, double v1, double w0, double w1, in ShapeBrush b,
                               BoxFaces faces = BoxFaces.All)
        {
            SacredDraw.Box(m, k, u0, u1, v0, v1, w0, w1, b, faces);
        }

        /// <summary>A vertical panel facing +w.</summary>
        public static void Panel(MeshData m, in Affine3 k, double u0, double v0, double u1, double v1, double w, in ShapeBrush b)
        {
            SacredDraw.Panel(m, k, u0, v0, u1, v1, w, b);
        }

        /// <summary>A flat horizontal rectangle (u0..u1, w0..w1) at height v facing up.</summary>
        public static void Floor(MeshData m, in Affine3 k, double u0, double u1, double w0, double w1, double v, in ShapeBrush b)
        {
            SacredDraw.Quad(m, k, u0, v, w0, u1, v, w0, u1, v, w1, u0, v, w1, 0, 1, 0, b);
        }

        /// <summary>A horizontal band along a wall (string course, sill, cornice step) from u0 to u1, v0 to v1, standing out
        /// <paramref name="proud"/> from the wall.</summary>
        public static void Band(MeshData m, in Affine3 k, double u0, double u1, double v0, double v1, double proud, in ShapeBrush b)
        {
            SacredDraw.Box(m, k, u0, u1, v0, v1, 0, proud, b, proud > 0.04 ? BoxFaces.Wall | BoxFaces.Bottom : BoxFaces.Front | BoxFaces.Top);
        }

        /// <summary>A stepped wooden cornice under an eave: three bands stepping out, the lowest at v.</summary>
        public static void Cornice(MeshData m, in Affine3 k, double u0, double u1, double v, double h, in ShapeBrush b, int lod)
        {
            if (lod >= 2)
            {
                Band(m, k, u0, u1, v, v + h, 0.12, b);
                return;
            }
            double s = h / 3;
            Band(m, k, u0, u1, v, v + s, 0.06, b);
            Band(m, k, u0 - 0.04, u1 + 0.04, v + s, v + 2 * s, 0.12, Tint(b, 1.12f));
            Band(m, k, u0 - 0.08, u1 + 0.08, v + 2 * s, v + h, 0.19, b);
            if (lod > 0) return;
            // A row of small carved blocks (dentils) on the middle step.
            int n = Math.Max(2, (int)((u1 - u0) / 0.45));
            for (int i = 0; i < n; i++)
            {
                double u = u0 + (u1 - u0) * (i + 0.5) / n;
                SacredDraw.Box(m, k, u - 0.07, u + 0.07, v + s + 0.03, v + 2 * s - 0.03, 0.12, 0.17, b, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            }
        }

        // =================================================================================================================
        // Windows and doors (face frames; centred at u, sill at v0 unless stated)
        // =================================================================================================================

        /// <summary>A carved lattice window (tiki jhyā) with its centre at (u, vc): LOD0 the frame with ears and the
        /// lattice, LOD1 the frame, LOD2 the dark opening.</summary>
        public static void Lattice(MeshData m, in Affine3 k, double u, double vc, double ww, double wh, int lod, int bars = 3)
        {
            if (lod >= 2)
            {
                Panel(m, k, u - 0.5 * ww, vc - 0.5 * wh, u + 0.5 * ww, vc + 0.5 * wh, 0.02, Recess);
                return;
            }
            SacredParts.LatticeWindow(m, k, u, vc, 0, ww, wh, Wood, lod, bars);
        }

        /// <summary>A Newar window with a cusped arch head (the upper windows of Kumari Ghar and the palace courts): a carved
        /// frame with ears, the lattice and the lobed arch over it.</summary>
        public static void ArchWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, int lod)
        {
            if (lod >= 2)
            {
                Panel(m, k, u - 0.5 * ww, v0, u + 0.5 * ww, v0 + wh, 0.02, Recess);
                return;
            }
            double archRise = 0.36 * ww;
            SacredParts.LatticeWindow(m, k, u, v0 + 0.5 * (wh - archRise), 0, ww, wh - archRise, Wood, lod, lod == 0 ? 3 : 0);
            ShikharaGenerator.Arch(m, k, u, v0 + wh - archRise, 0.02, ww, archRise, 0.09, 0.08, true, Wood, lod);
            Panel(m, k, u - 0.5 * ww, v0 + wh - archRise, u + 0.5 * ww, v0 + wh - 0.05, 0.015, Recess);
        }

        /// <summary>A projecting three-bay carved window (sāḥ jhyā) on the wall from sill v0 (gilt when asked).</summary>
        public static void Bay(MeshData m, in Affine3 k, double u, double v0, double width, double height, bool gilt, int lod)
        {
            if (lod >= 2)
            {
                Box(m, k, u - 0.5 * width, u + 0.5 * width, v0, v0 + height, 0, 0.3, gilt ? Gilt : Wood, BoxFaces.Front | BoxFaces.Top);
                return;
            }
            SacredParts.BayWindow(m, k, u, v0, 0, width, height, gilt, lod);
        }

        /// <summary>
        /// A Rana window of the white ranges: a moulded plaster surround with a hood, the glazing and a pair of louvred
        /// shutter leaves standing open against the wall (or closed when <paramref name="closed"/>), sill at v0.
        /// </summary>
        public static void RanaWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, in ShapeBrush shutter, bool closed, int lod)
        {
            double hu = 0.5 * ww;
            if (lod >= 2)
            {
                Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.02, closed ? shutter : Recess);
                return;
            }
            // Surround: jambs, sill and a hood moulding.
            Box(m, k, u - hu - 0.14, u - hu, v0, v0 + wh, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u + hu, u + hu + 0.14, v0, v0 + wh, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u - hu - 0.2, u + hu + 0.2, v0 - 0.12, v0, 0, 0.1, WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u - hu - 0.22, u + hu + 0.22, v0 + wh, v0 + wh + 0.16, 0, 0.12, WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            if (closed)
            {
                Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.025, shutter);
                if (lod == 0)
                {
                    // Louvres: a few dark slats and the meeting stile.
                    for (int i = 1; i < 6; i++)
                    {
                        double v = v0 + wh * i / 6.0;
                        Box(m, k, u - hu + 0.04, u + hu - 0.04, v - 0.025, v + 0.01, 0.025, 0.045, Tint(shutter, 0.72f), BoxFaces.Front | BoxFaces.Top);
                    }
                    Box(m, k, u - 0.025, u + 0.025, v0, v0 + wh, 0.025, 0.05, Tint(shutter, 0.8f), BoxFaces.Front);
                }
                return;
            }
            Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.012, Glass);
            if (lod == 0) Box(m, k, u - 0.025, u + 0.025, v0, v0 + wh, 0.012, 0.04, WhiteTrim, BoxFaces.Front);
            // Open leaves folded back against the wall beside the surround.
            double lw = 0.5 * ww;
            Box(m, k, u - hu - 0.16 - lw, u - hu - 0.16, v0 + 0.02, v0 + wh - 0.02, 0.0, 0.05, shutter, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u + hu + 0.16, u + hu + 0.16 + lw, v0 + 0.02, v0 + wh - 0.02, 0.0, 0.05, shutter, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod > 0) return;
            for (int s = -1; s <= 1; s += 2)
            {
                double c = u + s * (hu + 0.16 + 0.5 * lw);
                for (int i = 1; i < 5; i++)
                {
                    double v = v0 + wh * i / 5.0;
                    Box(m, k, c - 0.42 * lw, c + 0.42 * lw, v - 0.02, v + 0.01, 0.05, 0.065, Tint(shutter, 0.7f), BoxFaces.Front | BoxFaces.Top);
                }
            }
        }

        /// <summary>A blind cusped (Mughal) arch in low relief on a white wall, from v0 to v0 + h, width w.</summary>
        public static void BlindArch(MeshData m, in Affine3 k, double u, double v0, double width, double h, int lod)
        {
            if (lod >= 2) return;
            double r = 0.5 * width, rise = 0.55 * r, pier = h - rise;
            // The recessed field (a shade darker) and its frame.
            Panel(m, k, u - r + 0.08, v0, u + r - 0.08, v0 + pier, 0.008, WhiteRelief);
            Box(m, k, u - r, u - r + 0.08, v0, v0 + pier, 0, 0.035, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u + r - 0.08, u + r, v0, v0 + pier, 0, 0.035, WhiteTrim, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, k, u, v0 + pier, 0.0, width - 0.16, rise, 0.08, 0.035, true, WhiteTrim, lod + (lod == 0 ? 1 : 0));
        }

        /// <summary>
        /// A doorway on the wall from v0: the dark opening (a passage, never a sanctum), a carved timber frame of stepped
        /// jambs with ears and a lintel; optionally a gilt torana over it. Returns its top.
        /// </summary>
        public static double Doorway(MeshData m, in Affine3 k, double u, double v0, double dw, double dh, in ShapeBrush frame, bool torana, int lod)
        {
            Panel(m, k, u - 0.5 * dw, v0, u + 0.5 * dw, v0 + dh, 0.012, Recess);
            if (lod >= 2) return v0 + dh;
            int layers = lod == 0 ? 2 : 1;
            double jw = 0.13, top = v0 + dh;
            for (int j = 0; j < layers; j++)
            {
                double inner = 0.5 * dw + j * jw, outer = inner + jw, proud = 0.14 - 0.04 * j, ear = 0.22 + 0.1 * j, lt = top + (j + 1) * jw;
                ShapeBrush b = j % 2 == 0 ? frame : Tint(frame, 1.15f);
                Box(m, k, u - outer, u - inner, v0, lt - jw, 0, proud, b, BoxFaces.Wall);
                Box(m, k, u + inner, u + outer, v0, lt - jw, 0, proud, b, BoxFaces.Wall);
                Box(m, k, u - outer - ear, u + outer + ear, lt - jw, lt, 0, proud, b, BoxFaces.Wall | BoxFaces.Bottom);
            }
            double t = top + layers * jw;
            if (torana) t = SacredParts.Torana(m, k, u, t + 0.02, 0.02, dw + 0.5, Gilt, lod);
            return t;
        }

        // =================================================================================================================
        // Galleries, colonnades, railings
        // =================================================================================================================

        /// <summary>
        /// The continuous black timber gallery of the white ranges (§3.3): a floor projecting <paramref name="depth"/> at v0,
        /// carried on black diagonal struts from the wall, posts every <paramref name="pitch"/> metres, a balustrade of
        /// lattice panels, a top beam at v0 + h under the eave.
        /// </summary>
        public static void Gallery(MeshData m, in Affine3 k, double u0, double u1, double v0, double h, double depth, double pitch, int lod)
        {
            if (lod >= 2)
            {
                Box(m, k, u0, u1, v0 - 0.25, v0 + 0.95, 0, depth, GalleryWood, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                Box(m, k, u0, u1, v0 + h - 0.22, v0 + h, 0, depth, GalleryBeam, BoxFaces.Front | BoxFaces.Bottom);
                return;
            }
            // Floor slab with its fascia, and the top beam under the eave.
            Box(m, k, u0, u1, v0 - 0.2, v0, 0, depth, GalleryBeam, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Box(m, k, u0, u1, v0 + h - 0.18, v0 + h, 0, depth + 0.04, GalleryBeam, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            // Balustrade: a black lattice band with a top rail.
            Box(m, k, u0, u1, v0, v0 + 0.82, depth - 0.07, depth - 0.03, GalleryWood, BoxFaces.Front | BoxFaces.Back);
            Box(m, k, u0, u1, v0 + 0.82, v0 + 0.9, depth - 0.1, depth + 0.02, GalleryBeam, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            double p = lod == 0 ? pitch : 2 * pitch;
            int n = Math.Max(1, (int)Math.Round((u1 - u0) / p));
            for (int i = 0; i <= n; i++)
            {
                double u = u0 + (u1 - u0) * i / n;
                Box(m, k, u - 0.045, u + 0.045, v0 + 0.82, v0 + h - 0.18, depth - 0.12, depth - 0.03, GalleryWood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                if (lod == 0 && i < n)
                {
                    // Lattice cross-bars in the balustrade bay (light shows through between them).
                    double c = u + 0.5 * (u1 - u0) / n;
                    Box(m, k, c - 0.02, c + 0.02, v0 + 0.06, v0 + 0.8, depth - 0.03, depth, GalleryBeam, BoxFaces.Front);
                    Box(m, k, u + 0.05, u + (u1 - u0) / n - 0.05, v0 + 0.4, v0 + 0.44, depth - 0.03, depth, GalleryBeam, BoxFaces.Front);
                }
                // A slim black diagonal strut from the wall up to the floor's edge.
                if (lod <= 1) Strut(m, k, u, v0 - 1.35, 0.02, v0 - 0.2, depth - 0.12, 0.045, GalleryWood);
            }
        }

        /// <summary>A plain timber strut (square section, side 2·<paramref name="half"/>) from the wall at (u, va, wa) to
        /// (u, vb, wb).</summary>
        public static void Strut(MeshData m, in Affine3 k, double u, double va, double wa, double vb, double wb, double half, in ShapeBrush b)
        {
            double dv = vb - va, dw = wb - wa, l = Math.Sqrt(dv * dv + dw * dw);
            if (l < 1e-6) return;
            double cy = dv / l, sy = dw / l;
            // Local frame: x along u, y along the strut, z = x × y (out of the strut's face).
            Affine3 f = SacredDraw.Basis(k, u, va, wa, 1, 0, 0, 0, cy, sy);
            int first = m.VertexCount;
            for (int r = 0; r < 2; r++)
            {
                double y = r * l;
                for (int c = 0; c < 4; c++)
                {
                    double sx = c == 0 || c == 3 ? 1 : -1, sz = c < 2 ? 1 : -1;
                    SacredDraw.V(m, f, sx * half, y, sz * half, sx * 0.7, 0, sz * 0.7, b);
                }
            }
            SacredDraw.Grid(m, first, 2, 4, true, null);
        }

        /// <summary>A row of round columns with plain bases and capitals from v0 to v1 along a wall line at depth w.</summary>
        public static void Columns(MeshData m, in Affine3 k, double u0, double u1, int n, double w, double v0, double v1, double r, in ShapeBrush b, int lod)
        {
            for (int i = 0; i < n; i++)
            {
                double u = n == 1 ? 0.5 * (u0 + u1) : u0 + (u1 - u0) * i / (n - 1);
                if (lod >= 2)
                {
                    Box(m, k, u - r, u + r, v0, v1, w - r, w + r, b, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    continue;
                }
                Affine3 at = SacredDraw.At(k, u, v0, w);
                Shapes.Cylinder(m, SacredDraw.At(k, u, v0 + 0.18, w), b, r, v1 - v0 - 0.36, SacredParts.Seg(10, lod), 0, 0, false, false);
                Shapes.RoundedBox(m, SacredDraw.At(at, 0, 0.09, 0), b, 2.6 * r, 0.18, 2.6 * r, 0.03, 1);
                Shapes.RoundedBox(m, SacredDraw.At(k, u, v1 - 0.09, w), b, 2.8 * r, 0.18, 2.8 * r, 0.03, 1);
            }
        }

        /// <summary>A black iron railing from u0 to u1 at depth w: rails and bars every 0.15 m (LOD0), posts every 2 m.</summary>
        public static void Railing(MeshData m, in Affine3 k, double u0, double u1, double w, double v0, double h, int lod)
        {
            if (lod >= 2) return;
            Box(m, k, u0, u1, v0 + h - 0.05, v0 + h, w - 0.025, w + 0.025, Iron, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            Box(m, k, u0, u1, v0 + 0.12, v0 + 0.16, w - 0.02, w + 0.02, Iron, BoxFaces.Front | BoxFaces.Back);
            int n = Math.Max(1, (int)Math.Round((u1 - u0) / 2.0));
            for (int i = 0; i <= n; i++)
            {
                double u = u0 + (u1 - u0) * i / n;
                Box(m, k, u - 0.035, u + 0.035, v0, v0 + h + 0.06, w - 0.035, w + 0.035, Iron, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
            }
            if (lod > 0) return;
            int bars = Math.Max(2, (int)((u1 - u0) / 0.15));
            for (int i = 1; i < bars; i++)
            {
                double u = u0 + (u1 - u0) * i / bars;
                Box(m, k, u - 0.01, u + 0.01, v0 + 0.16, v0 + h - 0.05, w - 0.01, w + 0.01, Iron, BoxFaces.Front | BoxFaces.Back);
            }
        }

        // =================================================================================================================
        // Roofs
        // =================================================================================================================

        /// <summary>
        /// A tiled palace roof over a rectangle of half sizes (hu, hw) centred on the frame's origin (u, w), its eave at
        /// <paramref name="eaveV"/> standing <paramref name="over"/> metres out, rising to a ridge along the longer side at
        /// <paramref name="ridgeV"/> (hipped ends), with a fascia, a soffit with purlins back to the wall and an
        /// optional red valance. Returns the spec (for struts).
        /// </summary>
        public static RoofSpec Ridge(MeshData m, in Affine3 k, double hu, double hw, double eaveV, double ridgeV, double over, in ShapeBrush tile, bool fringe, int lod,
                                     double coursePitch = 1.1)
        {
            double eu = hu + over, ew = hw + over;
            double tu = Math.Max(0.12, eu - ew), tw = Math.Max(0.12, ew - eu);
            if (tu > 0.12 && tw > 0.12) tu = tw = 0.12;
            double t = (eu - hu) / Math.Max(1e-6, Math.Max(eu - tu, ew - tw));
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ew, EaveV = eaveV, TopU = tu, TopW = tw, TopV = ridgeV, WallU = hu, WallW = hw,
                WallTopV = Math.Max(eaveV + 0.05, eaveV + t * (ridgeV - eaveV) - 0.25), Lift = 0.02 * Math.Max(eu, ew), Sag = 0.006 * Math.Min(eu, ew),
                FasciaH = 0.22, CoursePitch = coursePitch, Gilt = false, Fringe = fringe, FringeH = 0.42, FringePitch = 0.5, Horns = false,
                Roof = tile, Ridge = Tint(tile, 0.78f), Fascia = Wood, Soffit = Ao(Wood, 0.55f), Horn = Wood,
            };
            SacredParts.Roof(m, k, r, Math.Min(2, lod));
            if (lod <= 1 && (tu > 0.12 || tw > 0.12))
            {
                var p = new Path3(2);
                if (tu > 0.12) p.Add(-tu, ridgeV + 0.06, 0).Add(tu, ridgeV + 0.06, 0);
                else p.Add(0, ridgeV + 0.06, -tw).Add(0, ridgeV + 0.06, tw);
                Shapes.Tube(m, k, r.Ridge, p, 0.1, 5, true);
            }
            return r;
        }

        /// <summary>
        /// One roof tier of a temple or tower (a skirt the next storey rises out of): eave ring (half sizes eu, ew at eaveV),
        /// the ring where it meets the storey above (tu, tw at topV), the wall below (wu, ww). <paramref name="metal"/> draws
        /// standing seams instead of tile courses (the copper roofs).
        /// </summary>
        public static RoofSpec Tier(MeshData m, in Affine3 k, double eu, double ew, double eaveV, double tu, double tw, double topV, double wu, double ww,
                                    in ShapeBrush roof, in ShapeBrush fascia, bool metal, bool fringe, int lod, double lift = 0.05)
        {
            double t = (eu - wu) / Math.Max(1e-6, eu - tu);
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ew, EaveV = eaveV, TopU = tu, TopW = tw, TopV = topV, WallU = wu, WallW = ww,
                WallTopV = Math.Max(eaveV + 0.05, eaveV + t * (topV - eaveV) - 0.22), Lift = lift * Math.Max(eu, ew), Sag = 0.015 * Math.Min(eu - tu, ew - tw),
                FasciaH = Math.Min(0.26, 0.1 + 0.012 * Math.Max(eu, ew)), CoursePitch = 0.75, Gilt = metal, Fringe = fringe, FringeH = 0.4, FringePitch = 0.5,
                Horns = lift > 0.02, Roof = roof, Ridge = metal ? CopperSeam : Tint(roof, 0.75f), Fascia = fascia, Soffit = Ao(Wood, 0.55f), Horn = fascia,
            };
            SacredParts.Roof(m, k, r, Math.Min(2, lod));
            return r;
        }

        /// <summary>Carved struts under a roof every <paramref name="pitch"/> metres along each wall (LOD0 and LOD1).</summary>
        public static int Struts(MeshData m, in Affine3 k, in RoofSpec r, double footV, double pitch, bool rich, uint seed, int lod)
        {
            if (lod >= 2) return 0;
            int pu = Math.Max(2, (int)Math.Round(2 * r.WallU / pitch)), pw = Math.Max(2, (int)Math.Round(2 * r.WallW / pitch));
            return SacredParts.Struts(m, k, r, footV, pu, pw, rich, Wood, seed, lod);
        }

        /// <summary>Hanging bells along the eaves (LOD0).</summary>
        public static int Bells(MeshData m, in Affine3 k, in RoofSpec r, double spacing, double size, int lod)
        {
            return lod == 0 ? SacredParts.Bells(m, k, r, spacing, size, true, lod) : 0;
        }

        /// <summary>A small white roof pavilion (dormer lantern) on a ridge at (u, v, w): a white box with a gabled tile cap.</summary>
        public static void Dormer(MeshData m, in Affine3 k, double u, double v, double w, double width, int lod)
        {
            double hw = 0.5 * width, h = 0.9 * width;
            Box(m, k, u - hw, u + hw, v - 0.6, v + h, w - 0.7 * hw, w + 0.7 * hw, White, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                Panel(m, k, u - 0.3 * hw, v + 0.2, u + 0.3 * hw, v + 0.8 * h, w + 0.7 * hw + 0.01, Glass);
            }
            Ridge(m, SacredDraw.At(k, u, 0, w), hw, 0.7 * hw, v + h, v + h + 0.55 * width, 0.25, Tile, false, Math.Max(1, lod));
        }

        // =================================================================================================================
        // Statues and small parts
        // =================================================================================================================

        /// <summary>A guardian lion on a moulded pedestal at (u, w) of a frame, facing +w; painted white with stripes when
        /// <paramref name="painted"/> (the gate lions), grey stone otherwise. h is the lion's height.</summary>
        public static void Lion(MeshData m, in Affine3 k, double u, double v, double w, double pedestal, double h, bool painted, int side, int lod)
        {
            SacredParts.Pedestal(m, k, u, v, w, 0.32 * h, 0.42 * h, pedestal, painted ? Dado : StoneGrey, Math.Min(2, lod));
            SacredFigures.Guardian(m, k, u, v + pedestal, w, h, GuardianKind.Lion, painted, SacredFigures.Hero, Math.Min(2, lod), side);
            if (!painted || lod > 0) return;
            // The red stripes of the Hanuman Dhoka and Singha Dhoka lions: thin bands round the legs and the chest.
            for (int s = -1; s <= 1; s += 2)
                for (int q = 0; q < 2; q++)
                    Shapes.Torus(m, SacredDraw.At(k, u + s * 0.11 * h, v + pedestal + (0.18 + 0.12 * q) * h, w + 0.08 * h), Dado, 0.06 * h, 0.012 * h, 8, 4);
        }

        /// <summary>A gilt finial (gajur) from v0 at (u, w).</summary>
        public static double Finial(MeshData m, in Affine3 k, double u, double v0, double w, double h, double r, int lod, bool taleju = false)
        {
            return SacredParts.Gajur(m, SacredDraw.At(k, u, 0, w), v0, h, r, Gilt, Math.Min(2, lod), taleju);
        }

        /// <summary>A potted plant (terracotta pot and a leafy crown) at (u, v, w).</summary>
        public static void PottedPlant(MeshData m, in Affine3 k, double u, double v, double w, double s, bool flowers, int lod)
        {
            if (lod >= 2) return;
            Shapes.Frustum(m, SacredDraw.At(k, u, v, w), Pot, 0.13 * s, 0.18 * s, 0.26 * s, 6, 0, 0, false, true);
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.36 * s, w), flowers ? Flowers : Leaves, 0.2 * s, 0.16 * s, 0.2 * s, 5, false);
        }

        /// <summary>A bell-and-lamp gilt pinnacle cluster (Kumari Ghar's ridge): three tall and two short gilt finials on a
        /// gilt base at (u, v, w).</summary>
        public static void PinnacleCluster(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            Box(m, k, u - 0.9, u + 0.9, v, v + 0.25, w - 0.35, w + 0.35, Gilt, BoxFaces.All & ~BoxFaces.Bottom);
            for (int i = -2; i <= 2; i++)
            {
                bool tall = i % 2 == 0;
                double hh = tall ? h : 0.7 * h, rr = tall ? 0.22 : 0.17;
                if (lod >= 2 && !tall) continue;
                Finial(m, k, u + 0.4 * i, v + 0.25, w, hh, rr, lod);
            }
        }

        /// <summary>A red cloth valance with a white top band hung straight along a wall line from u0 to u1 at v (top), depth w.</summary>
        public static void ValanceLine(MeshData m, in Affine3 k, double u0, double u1, double v, double w, double drop, int lod)
        {
            if (lod >= 2)
            {
                Panel(m, k, u0, v - drop, u1, v, w, Valance);
                return;
            }
            Panel(m, k, u0, v - 0.1, u1, v, w + 0.005, ValanceWhite);
            int n = Math.Max(2, (int)((u1 - u0) / (lod == 0 ? 0.5 : 1.2)));
            int first = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double u = u0 + (u1 - u0) * i / n, d = i % 2 == 0 ? drop : 0.72 * drop;
                SacredDraw.V(m, k, u, v - 0.1, w, 0, -0.1, 1, Valance);
                SacredDraw.V(m, k, u, v - d, w, 0, -0.1, 1, Valance);
            }
            for (int i = 0; i < n; i++) SacredDraw.Quad(m, first + 2 * i, first + 2 * i + 2, first + 2 * i + 3, first + 2 * i + 1);
        }
    }
}
