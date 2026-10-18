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
        public static readonly ShapeBrush Glass = B(0x1E2427, MaterialChannel.Glass);

        /// <summary>The orange-brown lattice of the round-headed ground-floor windows of the white Basantapur wing
        /// (basantapur_tower c_02).</summary>
        public static readonly ShapeBrush LatticeBrown = B(0x8A4A2C, MaterialChannel.WoodCarved);

        // --- Newar brick ranges and towers (§3.4, §3.11-3.16, §3.19) ----------------------------------------------------
        public static readonly ShapeBrush Brick = B(0x9C4A36, MaterialChannel.Brick);
        public static readonly ShapeBrush BrickNew = B(0xB5583E, MaterialChannel.Brick);

        /// <summary>The brick of the Lohan Chowk complex as rebuilt in 2017-2023 (basantapur_tower c_00): lighter and pinker
        /// than the old brick.</summary>
        public static readonly ShapeBrush BrickRebuilt = B(0xAA5843, MaterialChannel.Brick);
        public static readonly ShapeBrush BrickGlazed = B(0x9A4532, MaterialChannel.BrickGlazed);
        public static readonly ShapeBrush BrickKumari = B(0xA6563F, MaterialChannel.Brick);
        public static readonly ShapeBrush Wood = B(0x3B2A20, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodMid = B(0x5A3E2C, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodBeam = B(0x33251C, MaterialChannel.Wood);
        public static readonly ShapeBrush RedPost = B(0x9E3B2B, MaterialChannel.Paint);

        /// <summary>The dark of an open window or passage (not the sanctum colour).</summary>
        public static readonly ShapeBrush Recess = B(0x1C1511, MaterialChannel.Plain, 0.6f);

        // --- Roofs ----------------------------------------------------------------------------------------------------
        public static readonly ShapeBrush Tile = B(0x875A46, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileRidge = B(0x553829, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TileMoss = B(0x6B6047, MaterialChannel.RoofTile);
        public static readonly ShapeBrush TowerTile = B(0x8C5C49, MaterialChannel.RoofTile);
        public static readonly ShapeBrush Copper = B(0x6F8577, MaterialChannel.Metal);
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
        public static readonly ShapeBrush Cloak = B(0xD9541E, MaterialChannel.Fabric);
        public static readonly ShapeBrush Parasol = B(0x2A2422, MaterialChannel.Fabric);

        /// <summary>The grey metal canopy of Hanuman's parasol (hanuman_gate c_14).</summary>
        public static readonly ShapeBrush ParasolTop = B(0x5C5856, MaterialChannel.Metal);
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
            // A row of small carved blocks (dentils) on the middle step (front and underside: seen from the street).
            int n = Math.Max(2, (int)((u1 - u0) / 0.45));
            for (int i = 0; i < n; i++)
            {
                double u = u0 + (u1 - u0) * (i + 0.5) / n;
                SacredDraw.Box(m, k, u - 0.07, u + 0.07, v + s + 0.03, v + 2 * s - 0.03, 0.12, 0.17, b, BoxFaces.Front | BoxFaces.Bottom);
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
            LatticeFrame(m, k, u, vc, ww, wh, Wood, lod, bars);
        }

        /// <summary>The palace's lattice window body, built lean for the many hundred windows of the courts: the dark
        /// opening, slim jambs (no hidden tops), the head with its ears and the sill; LOD0 adds the lattice as thin proud bars
        /// (front faces only) in both directions.</summary>
        private static void LatticeFrame(MeshData m, in Affine3 k, double u, double vc, double ww, double wh, in ShapeBrush frame, int lod, int bars)
        {
            const double Fw = 0.08, Ear = 0.12;
            double hu = 0.5 * ww, hv = 0.5 * wh;
            Panel(m, k, u - hu, vc - hv, u + hu, vc + hv, 0.01, Recess);
            BoxFaces jamb = lod == 0 ? BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front;
            SacredDraw.Box(m, k, u - hu - Fw, u - hu, vc - hv, vc + hv, 0, 0.09, frame, jamb);
            SacredDraw.Box(m, k, u + hu, u + hu + Fw, vc - hv, vc + hv, 0, 0.09, frame, jamb);
            SacredDraw.Box(m, k, u - hu - Fw - Ear, u + hu + Fw + Ear, vc + hv, vc + hv + Fw, 0, 0.1, frame,
                           lod == 0 ? BoxFaces.Wall | BoxFaces.Bottom : BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            SacredDraw.Box(m, k, u - hu - Fw - Ear, u + hu + Fw + Ear, vc - hv - Fw, vc - hv, 0, 0.1, frame,
                           lod == 0 ? BoxFaces.Wall : BoxFaces.Front | BoxFaces.Top);
            if (lod >= 1 || bars <= 0) return;
            const double Bw = 0.025, Proud = 0.045;
            for (int i = 1; i <= bars; i++)
            {
                double uu = u - hu + ww * i / (bars + 1), vv = vc - hv + wh * i / (bars + 1);
                Panel(m, k, uu - Bw, vc - hv, uu + Bw, vc + hv, Proud, frame);
                Panel(m, k, u - hu, vv - Bw, u + hu, vv + Bw, Proud + 0.004, frame);
            }
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
            LatticeFrame(m, k, u, v0 + 0.5 * (wh - archRise), ww, wh - archRise, Wood, lod, lod == 0 ? 3 : 0);
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
            ShikharaGenerator.Arch(m, k, u, v0 + pier, 0.01, width - 0.16, rise, 0.08, 0.035, true, WhiteTrim, lod + (lod == 0 ? 1 : 0));
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

        /// <summary>
        /// A carved timber crest (the tympanum of a Newar window or door hood): a pointed arch with a scalloped (flame)
        /// edge, half width 0.5·<paramref name="width"/> and height <paramref name="h"/>, standing on its base line at
        /// (u, v) on the wall plane w, with a recessed inner field and a boss at LOD0. Cheap: about 60-150 triangles.
        /// Returns its top.
        /// </summary>
        public static double Crest(MeshData m, in Affine3 k, double u, double v, double w, double width, double h, in ShapeBrush b, int lod)
        {
            double r = 0.5 * width;
            int seg = lod == 0 ? 10 : lod == 1 ? 8 : 4;
            double[] x = ShapeScratch.U, z = ShapeScratch.W;
            for (int pass = 0; pass < (lod == 0 ? 2 : 1); pass++)
            {
                double sc = pass == 0 ? 1 : 0.72;
                int n = 0;
                for (int i = 0; i <= seg; i++)
                {
                    double a = Math.PI * i / seg, sa = Math.Sin(a);
                    double edge = pass == 0 && lod == 0 && i % 2 == 1 ? 0.93 : 1.0;
                    x[n] = sc * r * Math.Cos(a) * edge;
                    z[n] = -(sc * h * Math.Pow(sa, 0.8) * (0.86 + 0.14 * Math.Pow(sa, 14)) * edge);
                    n++;
                }
                // Basis: x = u, extrusion y = +w (outward), z = x × y = −v.
                Affine3 f = SacredDraw.Basis(k, u, v + (pass == 0 ? 0 : 0.06 * h), w + (pass == 0 ? 0 : 0.07), 1, 0, 0, 0, 0, 1);
                ShapeBrush br = pass == 0 ? b : Tint(b, 0.75f);
                if (lod >= 1 || pass == 1) Shapes.BevelExtrude(m, f, br, x, z, n, pass == 0 ? 0.08 : 0.03, 0, 0, BevelStyle.Chamfer, true, false);
                else Shapes.BevelExtrude(m, f, br, x, z, n, 0.08, 0.02, 1, BevelStyle.Chamfer, true, false);
            }
            if (lod == 0) Shapes.Sphere(m, SacredDraw.At(k, u, v + 0.42 * h, w + 0.1), b, Math.Min(0.09, 0.18 * r), 5);
            return v + h;
        }

        /// <summary>
        /// A big carved Newar storey window (the windows of the Basantapur faces, photos basantapur_tower c_00, c_10,
        /// lohan_south c_05): the dark opening with its lattice, a heavy dark frame, the lintel and the sill running out in
        /// long ears (the signature T-shape of a Newar window seen from the square), a second stepped moulding over the
        /// lintel and under the sill, and, with <paramref name="crest"/>, a carved crest over it. Centre u, sill v0.
        /// LOD0 about 90 triangles (plus the crest), LOD1 the frame, LOD2 the dark opening and the lintel line.
        /// </summary>
        public static void FramedWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, bool crest, int lod)
        {
            const double F = 0.13;
            double hu = 0.5 * ww, ear = Math.Min(0.5, 0.3 + 0.12 * ww), top = v0 + wh;
            if (lod >= 2)
            {
                Panel(m, k, u - hu - F, v0, u + hu + F, top, 0.02, Recess);
                Box(m, k, u - hu - F - ear, u + hu + F + ear, top, top + 0.16, 0, 0.12, Wood, BoxFaces.Front | BoxFaces.Top);
                return;
            }
            Panel(m, k, u - hu, v0, u + hu, top, 0.01, Recess);
            BoxFaces jamb = lod == 0 ? BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front;
            Box(m, k, u - hu - F, u - hu, v0, top, 0, 0.12, Wood, jamb);
            Box(m, k, u + hu, u + hu + F, v0, top, 0, 0.12, Wood, jamb);
            // Lintel and sill with their ears, and the stepped mouldings beyond them.
            Box(m, k, u - hu - F - ear, u + hu + F + ear, top, top + 0.17, 0, 0.15, Wood, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u - hu - F - ear, u + hu + F + ear, v0 - 0.17, v0, 0, 0.15, Wood, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod == 0)
            {
                Box(m, k, u - hu - F - 0.55 * ear, u + hu + F + 0.55 * ear, top + 0.17, top + 0.3, 0, 0.1, WoodMid, BoxFaces.Wall);
                Box(m, k, u - hu - F - 0.55 * ear, u + hu + F + 0.55 * ear, v0 - 0.3, v0 - 0.17, 0, 0.1, WoodMid, BoxFaces.Front | BoxFaces.Bottom);
                // The inner frame and the lattice (a close grid of slim bars, light between them).
                Box(m, k, u - hu, u + hu, top - 0.07, top, 0.01, 0.08, WoodMid, BoxFaces.Front | BoxFaces.Bottom);
                Box(m, k, u - hu, u + hu, v0, v0 + 0.07, 0.01, 0.08, WoodMid, BoxFaces.Front | BoxFaces.Top);
                int nu = Math.Max(3, (int)Math.Round(ww / 0.2)), nv = Math.Max(3, (int)Math.Round(wh / 0.2));
                for (int i = 1; i < nu; i++)
                {
                    double uu = u - hu + ww * i / nu;
                    Panel(m, k, uu - 0.022, v0 + 0.07, uu + 0.022, top - 0.07, 0.05, Wood);
                }
                for (int i = 1; i < nv; i++)
                {
                    double vv = v0 + wh * i / nv;
                    Panel(m, k, u - hu, vv - 0.022, u + hu, vv + 0.022, 0.054, Wood);
                }
            }
            if (crest) Crest(m, k, u, top + (lod == 0 ? 0.3 : 0.17), 0.02, ww + 0.25, 0.42 * ww + 0.15, Wood, lod);
        }

        /// <summary>A flat half disc (an arch head's field) on the wall plane w facing +w: centre (u, v), half width r,
        /// rise <paramref name="rise"/>, <paramref name="seg"/> segments.</summary>
        public static void HalfDisc(MeshData m, in Affine3 k, double u, double v, double r, double rise, double w, in ShapeBrush b, int seg)
        {
            int c = SacredDraw.V(m, k, u, v, w, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double t = Math.PI * i / seg;
                SacredDraw.V(m, k, u + r * Math.Cos(t), v + rise * Math.Sin(t), w, 0, 0, 1, b);
            }
            for (int i = 0; i < seg; i++) SacredDraw.Tri(m, c, first + i, first + i + 1);
        }

        /// <summary>A recessed rectangular plaster panel (the blind panels of the Rana fronts on Basantapur, basantapur_tower
        /// c_02): a shade darker field inside a slim raised frame.</summary>
        public static void BlindPanel(MeshData m, in Affine3 k, double u, double v0, double pw, double ph, int lod)
        {
            if (lod >= 2) return;
            double hu = 0.5 * pw;
            Panel(m, k, u - hu + 0.06, v0 + 0.06, u + hu - 0.06, v0 + ph - 0.06, 0.006, WhiteRelief);
            if (lod > 0) return;
            Box(m, k, u - hu, u + hu, v0, v0 + 0.06, 0, 0.03, WhiteTrim, BoxFaces.Front | BoxFaces.Top);
            Box(m, k, u - hu, u + hu, v0 + ph - 0.06, v0 + ph, 0, 0.03, WhiteTrim, BoxFaces.Front | BoxFaces.Bottom);
            Box(m, k, u - hu, u - hu + 0.06, v0 + 0.06, v0 + ph - 0.06, 0, 0.03, WhiteTrim, BoxFaces.Front | BoxFaces.Right);
            Box(m, k, u + hu - 0.06, u + hu, v0 + 0.06, v0 + ph - 0.06, 0, 0.03, WhiteTrim, BoxFaces.Front | BoxFaces.Left);
        }

        /// <summary>
        /// A round-headed ground-floor window of the Rana wings on Basantapur (basantapur_tower c_02, c_10): a white
        /// moulded surround with a semicircular head over an orange-brown lattice. Sill at v0.
        /// </summary>
        public static void ArchedLatticeWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, int lod)
        {
            double hu = 0.5 * ww, rect = wh - hu;
            if (lod >= 2)
            {
                Panel(m, k, u - hu, v0, u + hu, v0 + rect, 0.02, LatticeBrown);
                return;
            }
            Panel(m, k, u - hu, v0, u + hu, v0 + rect, 0.008, LatticeBrown);
            HalfDisc(m, k, u, v0 + rect, hu, hu, 0.008, LatticeBrown, lod == 0 ? 8 : 5);
            Box(m, k, u - hu - 0.12, u - hu, v0, v0 + rect, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u + hu, u + hu + 0.12, v0, v0 + rect, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u - hu - 0.16, u + hu + 0.16, v0 - 0.1, v0, 0, 0.09, WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            ShikharaGenerator.Arch(m, k, u, v0 + rect, 0.012, ww + 0.12, hu + 0.06, 0.12, 0.06, false, WhiteTrim, lod + 1);
            if (lod > 0) return;
            int n = Math.Max(2, (int)Math.Round(ww / 0.16));
            for (int i = 1; i < n; i++)
            {
                double uu = u - hu + ww * i / n;
                Panel(m, k, uu - 0.015, v0, uu + 0.015, v0 + rect, 0.02, Tint(LatticeBrown, 0.7f));
            }
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
                if (lod >= 1)
                {
                    // Square base and capital blocks.
                    Box(m, k, u - 1.3 * r, u + 1.3 * r, v0, v0 + 0.18, w - 1.3 * r, w + 1.3 * r, b, BoxFaces.All & ~BoxFaces.Bottom);
                    Box(m, k, u - 1.4 * r, u + 1.4 * r, v1 - 0.18, v1, w - 1.4 * r, w + 1.4 * r, b, BoxFaces.All & ~BoxFaces.Top);
                    continue;
                }
                Shapes.RoundedBox(m, SacredDraw.At(at, 0, 0.09, 0), b, 2.6 * r, 0.18, 2.6 * r, 0.03, 1);
                Shapes.RoundedBox(m, SacredDraw.At(k, u, v1 - 0.09, w), b, 2.8 * r, 0.18, 2.8 * r, 0.03, 1);
            }
        }

        /// <summary>
        /// A carved figure strut (tundal) from the wall at (u, va, wa) up and out to (u, vb, wb): a broad timber strut, at
        /// LOD0 a standing deity figure carved on its outer face (a rounded body on a lotus block with a round head and a
        /// small arched halo) and the bracket block at its foot; LOD1 the strut and the figure as one block.
        /// </summary>
        public static void FigureStrut(MeshData m, in Affine3 k, double u, double va, double wa, double vb, double wb, int lod)
        {
            if (lod >= 2) return;
            Strut(m, k, u, va, wa, vb, wb, 0.085, Wood);
            double dv = vb - va, dw = wb - wa, l = Math.Sqrt(dv * dv + dw * dw);
            if (l < 0.3) return;
            double cy = dv / l, sy = dw / l;
            // Frame along the strut: x along u, y up the strut, z out of its lower face.
            Affine3 f = SacredDraw.Basis(k, u, va, wa, 1, 0, 0, 0, cy, sy);
            if (lod == 1)
            {
                SacredDraw.Box(m, f, -0.11, 0.11, 0.3 * l, 0.8 * l, 0.06, 0.16, WoodMid, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                return;
            }
            Shapes.RoundedBox(m, SacredDraw.At(f, 0, 0.27 * l, 0.11), Wood, 0.24, 0.12, 0.1, 0.03, 1);
            Shapes.RoundedBox(m, SacredDraw.At(f, 0, 0.5 * l, 0.12), WoodMid, 0.2, 0.36 * l, 0.1, 0.05, 1);
            Shapes.Sphere(m, SacredDraw.At(f, 0, 0.73 * l, 0.13), WoodMid, 0.075, 6);
            ShikharaGenerator.Arch(m, f, 0, 0.7 * l, 0.1, 0.26, 0.15, 0.04, 0.02, false, Wood, 1);
            Box(m, k, u - 0.13, u + 0.13, va - 0.14, va + 0.02, 0, 0.16, Wood, BoxFaces.Wall | BoxFaces.Bottom);
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
                                     double coursePitch = 1.7)
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
                                    in ShapeBrush roof, in ShapeBrush fascia, bool metal, bool fringe, int lod, double lift = 0.05, double sag = 0.015)
        {
            double t = (eu - wu) / Math.Max(1e-6, eu - tu);
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ew, EaveV = eaveV, TopU = tu, TopW = tw, TopV = topV, WallU = wu, WallW = ww,
                WallTopV = Math.Max(eaveV + 0.05, eaveV + t * (topV - eaveV) - 0.22), Lift = lift * Math.Max(eu, ew), Sag = sag * Math.Min(eu - tu, ew - tw),
                FasciaH = Math.Min(0.26, 0.1 + 0.012 * Math.Max(eu, ew)), CoursePitch = metal ? 0 : 1.05, Gilt = false, Fringe = fringe, FringeH = 0.4, FringePitch = 0.5,
                Horns = lift > 0.02, Roof = roof, Ridge = metal ? CopperSeam : Tint(roof, 0.75f), Fascia = fascia, Soffit = Ao(Wood, 0.55f), Horn = fascia,
            };
            SacredParts.Roof(m, k, r, Math.Min(2, lod));
            // Copper sheet: standing seams that follow the sagging slope (the kit's straight seams would float over it).
            if (metal && lod == 0) Seams(m, k, r, 1.9);
            return r;
        }

        /// <summary>A quad wound so its front faces the given normal (whatever order the corners come in).</summary>
        public static void FacingQuad(MeshData m, in Affine3 k, double u0, double v0, double w0, double u1, double v1, double w1, double u2, double v2,
                                      double w2, double u3, double v3, double w3, double nu, double nv, double nw, in ShapeBrush b)
        {
            double ax = u1 - u0, ay = v1 - v0, az = w1 - w0, bx = u2 - u0, by = v2 - v0, bz = w2 - w0;
            double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
            if (cx * nu + cy * nv + cz * nw >= 0) SacredDraw.Quad(m, k, u0, v0, w0, u1, v1, w1, u2, v2, w2, u3, v3, w3, nu, nv, nw, b);
            else SacredDraw.Quad(m, k, u3, v3, w3, u2, v2, w2, u1, v1, w1, u0, v0, w0, nu, nv, nw, b);
        }

        /// <summary>The standing seams of a copper roof: low ribs (a two-faced ridge) down the slope about every
        /// <paramref name="pitch"/> metres, sampled along the curved slope so they lie on it.</summary>
        public static void Seams(MeshData m, in Affine3 k, in RoofSpec r, double pitch)
        {
            ShapeBrush b = r.Ridge;
            const int Steps = 4;
            const double Half = 0.035, Rise = 0.045;
            for (int s = 0; s < 4; s++)
            {
                double len = 2 * (s % 2 == 0 ? r.EaveU : r.EaveW);
                int n = Math.Max(3, (int)Math.Round(len / pitch));
                double su = s == 0 ? 1 : s == 2 ? -1 : 0, sw = s == 1 ? 1 : s == 3 ? -1 : 0;
                for (int j = 0; j < n; j++)
                {
                    double a = -0.95 + 1.9 * (j + 0.5) / n;
                    double pu = 0, pv = 0, pw = 0, pnu = 0, pnv = 0, pnw = 0;
                    for (int q = 0; q <= Steps; q++)
                    {
                        double tt = 0.02 + 0.95 * q / Steps, u, v, w, nu, nv, nw;
                        SacredParts.RoofPoint(r, s, a, tt, out u, out v, out w);
                        SacredParts.RoofNormal(r, s, a, tt, out nu, out nv, out nw);
                        if (q > 0)
                        {
                            // Left and right faces of the rib between the previous sample and this one.
                            for (int side = -1; side <= 1; side += 2)
                            {
                                double ex = side * Half;
                                double a0u = pu + su * ex, a0v = pv, a0w = pw + sw * ex, a1u = u + su * ex, a1v = v, a1w = w + sw * ex;
                                double t0u = pu + Rise * pnu, t0v = pv + Rise * pnv, t0w = pw + Rise * pnw;
                                double t1u = u + Rise * nu, t1v = v + Rise * nv, t1w = w + Rise * nw;
                                double fu = 0.5 * (pnu + nu) + side * su, fv = 0.5 * (pnv + nv), fw = 0.5 * (pnw + nw) + side * sw;
                                FacingQuad(m, k, a0u, a0v, a0w, a1u, a1v, a1w, t1u, t1v, t1w, t0u, t0v, t0w, fu, fv, fw, b);
                            }
                        }
                        pu = u; pv = v; pw = w; pnu = nu; pnv = nv; pnw = nw;
                    }
                }
            }
        }

        /// <summary>
        /// Dense dark rafters under a deep temple soffit (Taleju's lowest roofs, photo taleju c_25): thin battens from the
        /// fascia back to the wall every <paramref name="pitch"/> metres on all four sides, hanging a hand under the soffit.
        /// The roof's rings are the arguments of <see cref="Tier"/>. LOD0 only.
        /// </summary>
        public static void SoffitRafters(MeshData m, in Affine3 k, double eu, double ew, double eaveV, double tu, double tw, double topV, double wu, double ww,
                                         double pitch, int lod)
        {
            if (lod > 0) return;
            double t = (eu - wu) / Math.Max(1e-6, eu - tu);
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ew, EaveV = eaveV, TopU = tu, TopW = tw, TopV = topV, WallU = wu, WallW = ww,
                WallTopV = Math.Max(eaveV + 0.05, eaveV + t * (topV - eaveV) - 0.22), FasciaH = Math.Min(0.26, 0.1 + 0.012 * Math.Max(eu, ew)),
            };
            ShapeBrush b = Ao(WoodBeam, 0.5f);
            for (int s = 0; s < 4; s++)
            {
                double len = 2 * (s % 2 == 0 ? eu : ew);
                int n = Math.Max(4, (int)Math.Round(len / pitch));
                double ou, ow;
                SacredParts.Out(s, out ou, out ow);
                for (int i = 1; i < n; i++)
                {
                    double a = -1 + 2.0 * i / n, pu, pv, pw, qu, qw;
                    SacredParts.RoofPoint(r, s, a, 0, out pu, out pv, out pw);
                    pv -= r.FasciaH + 0.02;
                    SacredParts.OnRect(s, a, wu, ww, out qu, out qw);
                    double qv = r.WallTopV - 0.02;
                    // Along-eave direction for the batten's width.
                    double su = Math.Abs(ow), sw = Math.Abs(ou), hw = 0.025;
                    SacredDraw.Quad(m, k, pu - su * hw, pv - 0.07, pw - sw * hw, pu + su * hw, pv - 0.07, pw + sw * hw, qu + su * hw, qv - 0.07, qw + sw * hw,
                                    qu - su * hw, qv - 0.07, qw - sw * hw, 0, -1, 0, b);
                }
            }
        }

        /// <summary>Carved struts under a roof every <paramref name="pitch"/> metres along each wall (LOD0 and LOD1).</summary>
        public static int Struts(MeshData m, in Affine3 k, in RoofSpec r, double footV, double pitch, bool rich, uint seed, int lod)
        {
            if (lod >= 2) return 0;
            // The struts' feet sit on the cornice a little under the soffit (the photographed struts are about as long as
            // the eave is deep), never down the wall.
            double over = Math.Max(r.EaveU - r.WallU, r.EaveW - r.WallW);
            footV = Math.Max(footV, r.WallTopV - (1.15 * over + 0.45));
            int pu = Math.Max(2, (int)Math.Round(2 * r.WallU / pitch)), pw = Math.Max(2, (int)Math.Round(2 * r.WallW / pitch));
            return SacredParts.Struts(m, k, r, footV, pu, pw, rich, Wood, seed, lod);
        }

        /// <summary>Hanging bells along the eaves (LOD0).</summary>
        public static int Bells(MeshData m, in Affine3 k, in RoofSpec r, double spacing, double size, int lod)
        {
            return lod == 0 ? SacredParts.Bells(m, k, r, spacing, size, true, lod) : 0;
        }

        /// <summary>
        /// A pent roof (a lean-to tile skirt) along a wall from u0 to u1, the mid-height roofs of the Lohan south range and
        /// the white Basantapur wing (basantapur_tower c_02, c_10, lohan_south c_00): the tiled slope from the wall at
        /// <paramref name="topV"/> (slightly concave) down to the eave at <paramref name="eaveV"/> standing
        /// <paramref name="over"/> metres out, the wooden fascia, the dark soffit back to the wall, boards closing the ends,
        /// tile courses (LOD0) and struts from <paramref name="footV"/> on the wall up under the soffit every
        /// <paramref name="strutPitch"/> metres (LOD0-1; carved figure struts when <paramref name="carved"/>).
        /// </summary>
        public static void Pent(MeshData m, in Affine3 k, double u0, double u1, double topV, double eaveV, double over, double footV, double strutPitch,
                                in ShapeBrush tile, bool carved, int lod)
        {
            const double Fh = 0.2;
            double rise = topV - eaveV, midV = eaveV + 0.5 * rise - (lod <= 1 ? 0.05 * over : 0), midW = 0.5 * over;
            double nl = Math.Sqrt(over * over + rise * rise), nv = over / nl, nw = rise / nl;
            // The slope in two bands (concave), the upper a shade lighter.
            FacingQuad(m, k, u0, topV, 0, u1, topV, 0, u1, midV, midW, u0, midV, midW, 0, nv, nw, Tint(tile, 1.05f));
            FacingQuad(m, k, u0, midV, midW, u1, midV, midW, u1, eaveV, over, u0, eaveV, over, 0, nv * 0.9, nw * 1.1, tile);
            // Flashing along the wall, the fascia, the soffit and the end boards.
            Box(m, k, u0, u1, topV - 0.04, topV + 0.08, 0, 0.1, Tint(tile, 0.75f), BoxFaces.Front | BoxFaces.Top);
            Box(m, k, u0 - 0.03, u1 + 0.03, eaveV - Fh, eaveV + 0.02, over - 0.07, over + 0.01, Wood, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            double sv = topV - 0.35;
            SacredDraw.Quad(m, k, u0, eaveV - Fh, over - 0.04, u1, eaveV - Fh, over - 0.04, u1, sv, 0.0, u0, sv, 0.0, 0, -1, 0, Ao(Wood, 0.5f));
            if (lod <= 1)
                for (int e = 0; e < 2; e++)
                {
                    double u = e == 0 ? u0 : u1, n = e == 0 ? -1 : 1;
                    SacredDraw.Quad(m, k, u, topV, 0, u, midV, midW, u, eaveV - Fh, over - 0.04, u, sv, 0, n, 0, 0, Wood);
                    SacredDraw.Tri(m, k, u, midV, midW, u, eaveV, over, u, eaveV - Fh, over - 0.04, n, 0, 0, Wood);
                }
            if (lod == 0)
            {
                // Tile courses: low risers across the slope.
                for (int i = 1; i <= 4; i++)
                {
                    double t = i / 5.0, v = t < 0.5 ? topV + (midV - topV) * 2 * t : midV + (eaveV - midV) * (2 * t - 1), w = over * t;
                    FacingQuad(m, k, u0, v, w, u1, v, w, u1, v + 0.045, w - 0.02, u0, v + 0.045, w - 0.02, 0, 0.3, 1, Tint(tile, 0.8f));
                }
            }
            if (lod >= 2 || strutPitch <= 0) return;
            int n2 = Math.Max(1, (int)Math.Round((u1 - u0) / strutPitch));
            if (lod == 1) n2 = Math.Max(1, n2 / 2);
            for (int i = 0; i < n2; i++)
            {
                double u = u0 + (u1 - u0) * (i + 0.5) / n2;
                if (carved) FigureStrut(m, k, u, footV, 0.03, eaveV - Fh - 0.06, over - 0.3, lod);
                else Strut(m, k, u, footV, 0.03, eaveV - Fh - 0.06, over - 0.3, 0.05, i % 2 == 0 ? Wood : WoodMid);
            }
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
        /// <paramref name="painted"/> (the gate lions), grey stone otherwise (the pedestal lions on Basantapur). h is the lion's
        /// height; <paramref name="quality"/> is the figure quality (the generic figure but for the Hanuman Dhoka pair); <paramref name="stoneBase"/>
        /// puts painted lions on a grey stone pedestal (Kumari Ghar).</summary>
        public static void Lion(MeshData m, in Affine3 k, double u, double v, double w, double pedestal, double h, bool painted, int side, int lod,
                                int quality = SacredFigures.Generic, bool stoneBase = false)
        {
            SacredParts.Pedestal(m, k, u, v, w, 0.32 * h, 0.42 * h, pedestal, painted && !stoneBase ? Dado : StoneGrey, Math.Min(2, lod));
            SacredFigures.Guardian(m, k, u, v + pedestal, w, h, GuardianKind.Lion, painted, quality, Math.Min(2, lod), side);
            if (!painted || lod > 0) return;
            // The red stripes of the Hanuman Dhoka and Singha Dhoka lions: thin bands round the legs and the chest.
            for (int s = -1; s <= 1; s += 2)
                for (int q = 0; q < 2; q++)
                    Shapes.Torus(m, SacredDraw.At(k, u + s * 0.11 * h, v + pedestal + (0.18 + 0.12 * q) * h, w + 0.08 * h), Dado, 0.06 * h, 0.012 * h, 8, 4);
        }

        /// <summary>A gilt finial (gajur) from v0 at (u, w).</summary>
        public static double Finial(MeshData m, in Affine3 k, double u, double v0, double w, double h, double r, int lod, bool taleju = false)
        {
            // Small finials (under 2 m) are a gilt cone from LOD1 on; the big ones keep their rings to LOD1.
            int fl = lod >= 1 && h < 2.0 ? 2 : Math.Min(2, lod);
            return SacredParts.Gajur(m, SacredDraw.At(k, u, 0, w), v0, h, r, Gilt, fl, taleju);
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
                // The middle one carries the detail; the others one LOD coarser.
                Finial(m, k, u + 0.4 * i, v + 0.25, w, hh, rr, i == 0 ? lod : Math.Min(2, lod + 1));
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
