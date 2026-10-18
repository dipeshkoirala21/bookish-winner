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

        // --- Round r8 (judge findings): the colours measured on the photos ----------------------------------------------

        /// <summary>Taleju's body brick (taleju ref_00, ref_02): a darker brown-red than the palace's rebuilt ranges.</summary>
        public static readonly ShapeBrush BrickTaleju = B(0x7A3E30, MaterialChannel.Brick);

        /// <summary>The pale grey-blue painted pilasters of the Hanuman Dhoka gate (hanuman_gate c_06, c_14).</summary>
        public static readonly ShapeBrush GatePale = B(0x7FA6C4, MaterialChannel.Paint);

        /// <summary>The dark backing of a Newar lattice window (the holes of the screen) and the brown lattice bars over it
        /// (#5A3E2C): from the square the fine diagonal lattice reads as one brown screen, never as glass.</summary>
        public static readonly ShapeBrush LatticeBack = B(0x241912, MaterialChannel.WoodCarved, 0.75f);

        public static readonly ShapeBrush LatticeBar = B(0x5A3E2C, MaterialChannel.WoodCarved);

        /// <summary>The lattice screen seen from afar (LOD1-2): the bars and holes averaged.</summary>
        public static readonly ShapeBrush LatticeFar = B(0x3E2C21, MaterialChannel.WoodCarved);

        /// <summary>White jali (a pierced white screen) of the Rana windows on the palace fronts, and its shaded holes.</summary>
        public static readonly ShapeBrush Jali = B(0xF0EDE5, MaterialChannel.Plaster);

        public static readonly ShapeBrush JaliHole = B(0x6F6B66, MaterialChannel.Plain, 0.8f);

        /// <summary>The white louvred shutters of the white palace fronts (hanuman_gate c_14, dakha_north c_07).</summary>
        public static readonly ShapeBrush ShutterWhite = B(0xF1EFE8, MaterialChannel.Paint);

        /// <summary>The warm terracotta tile of Panch Mukhi's five round roofs (panchamukhi c_10).</summary>
        public static readonly ShapeBrush TileTerracotta = B(0xA0664A, MaterialChannel.RoofTile);

        /// <summary>The grey-green sheet metal of the hipped roof over the forecourt's west block (2026 imagery).</summary>
        public static readonly ShapeBrush MetalRoof = B(0x6E7F72, MaterialChannel.Metal);

        /// <summary>Marigold garlands (orange-yellow).</summary>
        public static readonly ShapeBrush Marigold = B(0xF0A21E, MaterialChannel.Foliage);

        /// <summary>The grey stone flags of the forecourt (hanuman_gate c_07, c_14) and their darker joints.</summary>
        public static readonly ShapeBrush FlagJoint = B(0x6F6B65, MaterialChannel.Flagstone);

        /// <summary>Recolour the vertices from <paramref name="first"/> on that carry colour <paramref name="from"/> (RGB) to
        /// <paramref name="to"/> (RGB), keeping alpha: used to repaint parts of the shared figures to the photographs.</summary>
        public static void Recolor(MeshData m, int first, uint from, uint to)
        {
            byte fr = (byte)(from >> 16), fg = (byte)(from >> 8), fb = (byte)from;
            byte tr = (byte)(to >> 16), tg = (byte)(to >> 8), tb = (byte)to;
            byte[] c = m.Colors;
            if (c == null) return;
            for (int i = first; i < m.VertexCount; i++)
            {
                int o = 4 * i;
                if (c[o] != fr || c[o + 1] != fg || c[o + 2] != fb) continue;
                c[o] = tr;
                c[o + 1] = tg;
                c[o + 2] = tb;
            }
        }

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
                Panel(m, k, u - 0.5 * ww, vc - 0.5 * wh, u + 0.5 * ww, vc + 0.5 * wh, 0.02, LatticeFar);
                return;
            }
            LatticeFrame(m, k, u, vc, ww, wh, Wood, lod, bars);
        }

        /// <summary>
        /// The palace's Newar lattice window (photos c_kumari, sweta_bhairav c_02/c_06, hanuman_gate c_14), built lean for
        /// the many hundred windows of the courts: a deep, thickly moulded dark frame (jambs up to 0.18 m) whose lintel and
        /// sill run out in long ears (the T-shape seen from the square), a stepped inner moulding (LOD0) and the screen: a
        /// fine 45° lattice of brown bars over a dark backing (LOD0; <paramref name="bars"/> × 2 bars each way, 4-6), one
        /// brown screen panel from LOD1. Never glass.
        /// </summary>
        private static void LatticeFrame(MeshData m, in Affine3 k, double u, double vc, double ww, double wh, in ShapeBrush frame, int lod, int bars)
        {
            double fw = Math.Min(0.18, 0.1 + 0.08 * Math.Min(ww, wh)), ear = Math.Min(0.35, 0.12 + 0.25 * ww), bh = Math.Min(0.16, 0.08 + 0.08 * ww);
            double hu = 0.5 * ww, hv = 0.5 * wh;
            Panel(m, k, u - hu, vc - hv, u + hu, vc + hv, 0.01, lod == 0 && bars > 0 ? LatticeBack : LatticeFar);
            BoxFaces jamb = lod == 0 ? BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front;
            SacredDraw.Box(m, k, u - hu - fw, u - hu, vc - hv, vc + hv, 0, 0.12, frame, jamb);
            SacredDraw.Box(m, k, u + hu, u + hu + fw, vc - hv, vc + hv, 0, 0.12, frame, jamb);
            SacredDraw.Box(m, k, u - hu - fw - ear, u + hu + fw + ear, vc + hv, vc + hv + bh, 0, 0.14, frame,
                           lod == 0 ? BoxFaces.Wall | BoxFaces.Bottom : BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            SacredDraw.Box(m, k, u - hu - fw - ear, u + hu + fw + ear, vc - hv - bh, vc - hv, 0, 0.14, frame,
                           lod == 0 ? BoxFaces.Wall | BoxFaces.Bottom : BoxFaces.Front | BoxFaces.Top);
            if (lod >= 1) return;
            // The stepped inner moulding: a slimmer, lighter ring standing a little less proud inside the frame.
            ShapeBrush mould = Tint(frame, 1.35f);
            const double Mw = 0.045, Mp = 0.07;
            Panel(m, k, u - hu, vc - hv, u - hu + Mw, vc + hv, Mp, mould);
            Panel(m, k, u + hu - Mw, vc - hv, u + hu, vc + hv, Mp, mould);
            Panel(m, k, u - hu + Mw, vc + hv - Mw, u + hu - Mw, vc + hv, Mp, mould);
            Panel(m, k, u - hu + Mw, vc - hv, u + hu - Mw, vc - hv + Mw, Mp, mould);
            if (bars <= 0) return;
            DiagonalLattice(m, k, u, vc, hu - Mw, hv - Mw, Math.Max(4, Math.Min(6, 2 * bars)), 0.04, 0.022, LatticeBar);
        }

        /// <summary>A 45° lattice of <paramref name="n"/> thin bars each way over the rectangle (u ± hu, vc ± hv), standing
        /// <paramref name="w"/> proud, bars <paramref name="half"/> × 2 wide, clipped to the rectangle (front faces only).</summary>
        public static void DiagonalLattice(MeshData m, in Affine3 k, double u, double vc, double hu, double hv, int n, double w, double half, in ShapeBrush b)
        {
            double span = hu + hv, step = 2 * span / (n + 1), d = half * 0.70710678;
            for (int fam = 0; fam < 2; fam++)
            {
                double sg = fam == 0 ? 1 : -1;
                for (int i = 1; i <= n; i++)
                {
                    // The line v = sg·u + c (local to the centre).
                    double c = -span + step * i;
                    double ua = Math.Max(-hu, sg > 0 ? -hv - c : c - hv), ub = Math.Min(hu, sg > 0 ? hv - c : c + hv);
                    if (ub - ua < 0.02) continue;
                    double va = sg * ua + c, vb = sg * ub + c;
                    // Offset across the bar: (−sg, 1)/√2 · half.
                    double ou = -sg * d, ov = d, ww = w + 0.004 * fam;
                    SacredDraw.Quad(m, k, u + ua - ou, vc + va - ov, ww, u + ub - ou, vc + vb - ov, ww, u + ub + ou, vc + vb + ov, ww, u + ua + ou, vc + va + ov,
                                    ww, 0, 0, 1, b);
                }
            }
        }

        /// <summary>A Newar window with a cusped arch head (the upper windows of Kumari Ghar and the palace courts): a carved
        /// frame with ears, the lattice and the lobed arch over it. <paramref name="rise"/> is the arch head's height as a
        /// fraction of the width (0.36 a low segmental head; 0.55 the round heads of Kumari Ghar's ground floor; about 0.7
        /// the tall pointed cusped heads of the Kirtipur drum). Heads above half the width get their ring carved in the
        /// lighter wood, so it reads against the dark lattice, and the lattice carried up into them.</summary>
        public static void ArchWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, int lod, double rise = 0.36)
        {
            if (lod >= 2)
            {
                Panel(m, k, u - 0.5 * ww, v0, u + 0.5 * ww, v0 + wh, 0.02, Recess);
                return;
            }
            double archRise = Math.Min(rise * ww, 0.5 * wh);
            bool tall = rise > 0.5;
            LatticeFrame(m, k, u, v0 + 0.5 * (wh - archRise), ww, wh - archRise, Wood, lod, lod == 0 ? 3 : 0);
            ShikharaGenerator.Arch(m, k, u, v0 + wh - archRise, 0.02, ww, archRise, tall ? 0.11 : 0.09, 0.08, true, tall ? WoodMid : Wood,
                                   lod == 0 && (ww >= 1.1 || tall) ? 0 : 1);
            HalfDisc(m, k, u, v0 + wh - archRise, 0.5 * ww, archRise, 0.015, lod == 0 ? LatticeBack : LatticeFar, lod == 0 ? 8 : 5);
            if (!tall || lod > 0) return;
            // The tall heads: the lattice carried up into the arch.
            DiagonalLattice(m, k, u, v0 + wh - 0.55 * archRise, 0.3 * ww, 0.4 * archRise, 3, 0.04, 0.018, LatticeBar);
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
        /// A Rana window of the white ranges (photos hanuman_gate c_14, dakha_north c_07): a moulded plaster surround with a
        /// hood moulding, sill at v0. Closed (<paramref name="closed"/>): louvred shutter leaves across it (white on the palace
        /// fronts). Open: the opening filled with a screen, never glass (a dark Newar lattice, or white jali when
        /// <paramref name="jali"/>), the louvred leaves folded back against the wall. <paramref name="hood"/> adds the cusped
        /// stucco hood of the gate front over the surround.
        /// </summary>
        public static void RanaWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, in ShapeBrush shutter, bool closed, int lod,
                                      bool jali = false, bool hood = false)
        {
            double hu = 0.5 * ww;
            if (lod >= 2)
            {
                Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.02, closed ? Tint(shutter, 0.8f) : jali ? JaliHole : LatticeFar);
                return;
            }
            // Surround: jambs, sill and a hood moulding.
            Box(m, k, u - hu - 0.14, u - hu, v0, v0 + wh, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u + hu, u + hu + 0.14, v0, v0 + wh, 0, 0.06, WhiteTrim, BoxFaces.Wall);
            Box(m, k, u - hu - 0.2, u + hu + 0.2, v0 - 0.12, v0, 0, 0.1, WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u - hu - 0.22, u + hu + 0.22, v0 + wh, v0 + wh + 0.16, 0, 0.12, WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            if (hood)
            {
                // The cusped stucco hood over the window (a lobed arch in relief with its shaded field).
                double hw = ww + 0.5, rise = 0.42 * hw, hv = v0 + wh + 0.16;
                HalfDisc(m, k, u, hv, 0.5 * hw - 0.08, rise - 0.08, 0.008, WhiteRelief, lod == 0 ? 10 : 6);
                ShikharaGenerator.Arch(m, k, u, hv, 0.01, hw - 0.16, rise - 0.08, 0.09, 0.05, true, WhiteTrim, lod + 1);
            }
            if (closed)
            {
                Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.025, shutter);
                if (lod == 0)
                {
                    // Louvres: slats across both leaves and the meeting stile.
                    int n = Math.Max(5, (int)Math.Round(wh / 0.16));
                    for (int i = 1; i < n; i++)
                    {
                        double v = v0 + wh * i / n;
                        Box(m, k, u - hu + 0.04, u + hu - 0.04, v - 0.025, v + 0.01, 0.025, 0.045, Tint(shutter, 0.78f), BoxFaces.Front | BoxFaces.Top);
                    }
                    Box(m, k, u - 0.025, u + 0.025, v0, v0 + wh, 0.025, 0.05, Tint(shutter, 0.85f), BoxFaces.Front);
                }
                return;
            }
            // The screen in the opening: white jali (white bars over shaded holes) or the dark Newar lattice.
            Panel(m, k, u - hu, v0, u + hu, v0 + wh, 0.012, jali ? JaliHole : lod == 0 ? LatticeBack : LatticeFar);
            if (lod == 0)
            {
                DiagonalLattice(m, k, u, v0 + 0.5 * wh, hu, 0.5 * wh, Math.Max(5, Math.Min(10, (int)Math.Round(wh / 0.17))), 0.03, jali ? 0.035 : 0.022,
                                jali ? Jali : LatticeBar);
                Box(m, k, u - 0.03, u + 0.03, v0, v0 + wh, 0.012, 0.05, jali ? WhiteTrim : Wood, BoxFaces.Front);
            }
            // Open leaves folded back against the wall beside the surround.
            double lw = 0.5 * ww;
            Box(m, k, u - hu - 0.16 - lw, u - hu - 0.16, v0 + 0.02, v0 + wh - 0.02, 0.0, 0.05, shutter, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u + hu + 0.16, u + hu + 0.16 + lw, v0 + 0.02, v0 + wh - 0.02, 0.0, 0.05, shutter, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod > 0) return;
            for (int s = -1; s <= 1; s += 2)
            {
                double c = u + s * (hu + 0.16 + 0.5 * lw);
                for (int i = 1; i < 6; i++)
                {
                    double v = v0 + wh * i / 6.0;
                    Box(m, k, c - 0.42 * lw, c + 0.42 * lw, v - 0.02, v + 0.01, 0.05, 0.065, Tint(shutter, 0.72f), BoxFaces.Front | BoxFaces.Top);
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
        public static void FramedWindow(MeshData m, in Affine3 k, double u, double v0, double ww, double wh, bool crest, int lod, int bays = 1, bool gilt = false)
        {
            const double F = 0.16;
            double hu = 0.5 * ww, ear = Math.Min(0.5, 0.3 + 0.12 * ww), top = v0 + wh;
            ShapeBrush frame = gilt ? GiltDeep : Wood, inner = gilt ? Gilt : WoodMid;
            if (lod >= 2)
            {
                Panel(m, k, u - hu - F, v0, u + hu + F, top, 0.02, gilt ? GiltDeep : LatticeFar);
                Box(m, k, u - hu - F - ear, u + hu + F + ear, top, top + 0.16, 0, 0.12, Wood, BoxFaces.Front | BoxFaces.Top);
                return;
            }
            Panel(m, k, u - hu, v0, u + hu, top, 0.01, lod == 0 ? LatticeBack : LatticeFar);
            BoxFaces jamb = lod == 0 ? BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front;
            Box(m, k, u - hu - F, u - hu, v0, top, 0, 0.14, frame, jamb);
            Box(m, k, u + hu, u + hu + F, v0, top, 0, 0.14, frame, jamb);
            // Lintel and sill with their long ears, and the stepped mouldings beyond them.
            Box(m, k, u - hu - F - ear, u + hu + F + ear, top, top + 0.17, 0, 0.17, Wood, BoxFaces.Wall | BoxFaces.Bottom);
            Box(m, k, u - hu - F - ear, u + hu + F + ear, v0 - 0.17, v0, 0, 0.17, Wood, BoxFaces.Wall | BoxFaces.Bottom);
            // The mullions of a three-bay window (posts between the lattices).
            for (int i = 1; i < bays; i++)
            {
                double uu = u - hu + ww * i / bays;
                Box(m, k, uu - 0.07, uu + 0.07, v0, top, 0, 0.12, frame, lod == 0 ? BoxFaces.Front | BoxFaces.Left | BoxFaces.Right : BoxFaces.Front);
            }
            if (lod == 0)
            {
                Box(m, k, u - hu - F - 0.55 * ear, u + hu + F + 0.55 * ear, top + 0.17, top + 0.3, 0, 0.11, WoodMid, BoxFaces.Wall);
                Box(m, k, u - hu - F - 0.55 * ear, u + hu + F + 0.55 * ear, v0 - 0.3, v0 - 0.17, 0, 0.11, WoodMid, BoxFaces.Front | BoxFaces.Bottom);
                // The stepped inner frame and the fine diagonal lattice of each bay.
                Box(m, k, u - hu, u + hu, top - 0.07, top, 0.01, 0.09, inner, BoxFaces.Front | BoxFaces.Bottom);
                Box(m, k, u - hu, u + hu, v0, v0 + 0.07, 0.01, 0.09, inner, BoxFaces.Front | BoxFaces.Top);
                Box(m, k, u - hu, u - hu + 0.06, v0 + 0.07, top - 0.07, 0.01, 0.09, inner, BoxFaces.Front | BoxFaces.Right);
                Box(m, k, u + hu - 0.06, u + hu, v0 + 0.07, top - 0.07, 0.01, 0.09, inner, BoxFaces.Front | BoxFaces.Left);
                double bw = ww / bays;
                for (int i = 0; i < bays; i++)
                {
                    double bu = u - hu + bw * (i + 0.5), bhu = 0.5 * bw - (bays > 1 ? 0.07 : 0.06), bhv = 0.5 * wh - 0.07;
                    DiagonalLattice(m, k, bu, v0 + 0.5 * wh, bhu, bhv, Math.Max(4, Math.Min(10, (int)Math.Round(2 * Math.Max(bhu, bhv) / 0.16))), 0.045, 0.022,
                                    LatticeBar);
                }
            }
            if (crest) Crest(m, k, u, top + (lod == 0 ? 0.3 : 0.17), 0.02, ww + 0.25, 0.42 * ww + 0.15, Wood, lod);
        }

        /// <summary>
        /// A semicircular carved torana over a Newar window (Kumari Ghar's main windows, c_kumari c_00): a dark wood fan
        /// tympanum of radius <paramref name="r"/> standing on its base line at (u, v), a moulded rim, radial carving ribs
        /// and a small boss (LOD0), the field and rim from LOD1. Returns its top.
        /// </summary>
        public static double FanTorana(MeshData m, in Affine3 k, double u, double v, double r, int lod)
        {
            int seg = lod == 0 ? 12 : lod == 1 ? 8 : 5;
            HalfDisc(m, k, u, v, r, r, 0.03, Tint(Wood, 0.85f), seg);
            if (lod >= 2) return v + r;
            ShikharaGenerator.Arch(m, k, u, v, 0.03, 2 * r - 0.16, r - 0.08, 0.12, 0.1, false, Wood, lod + 1);
            if (lod > 0) return v + r;
            // Radial ribs (the carved rays of the fan) and the inner band.
            const int Ribs = 9;
            for (int i = 0; i < Ribs; i++)
            {
                double a = Math.PI * (i + 0.5) / Ribs, c = Math.Cos(a), s = Math.Sin(a), r0 = 0.3 * r, r1 = 0.86 * r, t = 0.022;
                SacredDraw.Quad(m, k, u + r0 * c - t * s, v + r0 * s + t * c, 0.06, u + r1 * c - t * s, v + r1 * s + t * c, 0.06, u + r1 * c + t * s, v + r1 * s - t * c, 0.06,
                                u + r0 * c + t * s, v + r0 * s - t * c, 0.06, 0, 0, 1, WoodMid);
            }
            HalfDisc(m, k, u, v, 0.3 * r, 0.3 * r, 0.065, WoodMid, 6);
            Shapes.Sphere(m, SacredDraw.At(k, u, v + 0.16 * r, 0.09), GiltDeep, 0.07, 5);
            return v + r;
        }

        /// <summary>A seated deity figure (the gate panel's figures, hanuman_gate c_14) facing +w at (u, v, w): crossed
        /// legs, a rounded torso, arms, a head with a crown; <paramref name="arms"/> above 2 fans more arms out.</summary>
        public static void SeatedFigure(MeshData m, in Affine3 k, double u, double v, double w, double h, in ShapeBrush body, in ShapeBrush cloth, int arms, int lod)
        {
            if (lod >= 2) return;
            int sg = lod == 0 ? 8 : 5;
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.12 * h, w + 0.06 * h), cloth, 0.32 * h, 0.12 * h, 0.2 * h, sg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.4 * h, w), cloth, 0.17 * h, 0.22 * h, 0.12 * h, sg, false);
            Shapes.Sphere(m, SacredDraw.At(k, u, v + 0.72 * h, w + 0.01 * h), body, 0.1 * h, sg);
            Shapes.Cone(m, SacredDraw.At(k, u, v + 0.8 * h, w), Gilt, 0.08 * h, 0.2 * h, 6, 0, 0, false);
            if (lod > 0) return;
            int pairs = Math.Max(1, arms / 2);
            for (int i = 0; i < pairs; i++)
            {
                double spread = 0.18 * h + 0.07 * h * i, lift = 0.45 * h + 0.1 * h * i;
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Bar(m, k, body, u + s * 0.14 * h, v + 0.55 * h, w, u + s * (0.14 * h + spread), v + lift, w + 0.06 * h, 0.025 * h, 4);
            }
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
                if (i < n)
                {
                    // The dark lattice screen between the posts (hanuman_gate c_06, c_14: the gallery reads as a dark band
                    // with only a narrow open strip under the top beam), slightly behind the posts.
                    double bu0 = u + 0.045, bu1 = u + (u1 - u0) / n - 0.045, sv0 = v0 + 0.9, sv1 = v0 + h - 0.18 - 0.25;
                    if (sv1 > sv0 + 0.2) Panel(m, k, bu0, sv0, bu1, sv1, depth - 0.08, Ao(GalleryWood, 0.85f));
                    if (lod == 0)
                    {
                        // A carved mid rail and a lighter diagonal lattice over the lower half; the balustrade's cross-bars.
                        if (sv1 > sv0 + 0.2)
                        {
                            Panel(m, k, bu0, 0.5 * (sv0 + sv1) - 0.03, bu1, 0.5 * (sv0 + sv1) + 0.03, depth - 0.06, GalleryBeam);
                            DiagonalLattice(m, k, 0.5 * (bu0 + bu1), 0.25 * (3 * sv0 + sv1), 0.5 * (bu1 - bu0), 0.25 * (sv1 - sv0), 3, depth - 0.07, 0.016,
                                            Tint(GalleryWood, 1.6f));
                        }
                        double c = u + 0.5 * (u1 - u0) / n;
                        Box(m, k, c - 0.02, c + 0.02, v0 + 0.06, v0 + 0.8, depth - 0.03, depth, GalleryBeam, BoxFaces.Front);
                        Box(m, k, u + 0.05, u + (u1 - u0) / n - 0.05, v0 + 0.4, v0 + 0.44, depth - 0.03, depth, GalleryBeam, BoxFaces.Front);
                    }
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

        /// <summary>tan 60°: the struts stand at about 60° (taleju ref_00, ref_02; lalitpur_tower c_04).</summary>
        private const double Tan60 = 1.7320508;

        private static readonly ShapeBrush[] StrutPaint =
        {
            B(0x8E5B37, MaterialChannel.WoodCarved), B(0x7A4A2E, MaterialChannel.WoodCarved), B(0x6B4129, MaterialChannel.WoodCarved),
            B(0x845236, MaterialChannel.WoodCarved),
        };

        /// <summary>
        /// The carved struts (tunala) under a roof, as photographed on Taleju, the towers and the ranges (taleju ref_00,
        /// ref_02, lalitpur_tower c_04, kirtipur_tower): slender (about 0.15-0.25 m wide), steep (about 60°) and close-set
        /// every <paramref name="pitch"/> metres (0.8 m on the temples, 1.0 m on the ranges), each standing on the wall
        /// with its head tight under the soffit's inner purlin, reaching out only about 0.45 of the eave's depth (at most
        /// 1.25 m, so a strut is at most about 2.5 m long: the outer eave rests on the rafters), so under a deep roof they read as one dense dark carved band.
        /// <paramref name="footV"/> is the lowest their feet may stand (the storey's floor). The corners get the winged
        /// griffin struts along the diagonal (LOD0). LOD0 and LOD1 (every other strut, plain sections).
        /// </summary>
        public static int Struts(MeshData m, in Affine3 k, in RoofSpec r, double footV, double pitch, bool rich, uint seed, int lod)
        {
            if (lod >= 2) return 0;
            pitch = Math.Max(0.5, pitch);
            int drawn = 0;
            double eaveLow = r.EaveV - r.FasciaH;
            for (int corner = 0; corner <= 1; corner++)
            {
                if (corner == 1 && lod > 0) break;
                for (int s = 0; s < 4; s++)
                {
                    double along = s % 2 == 0 ? r.WallU : r.WallW, wall = s % 2 == 0 ? r.WallW : r.WallU;
                    double depth = s % 2 == 0 ? r.EaveW - r.WallW : r.EaveU - r.WallU, eaveAlong = s % 2 == 0 ? r.EaveU : r.EaveW;
                    if (depth < 0.25) continue;
                    double hr = Math.Max(0.35, Math.Min(0.45 * depth, 1.25)), f = hr / depth;
                    double size = Math.Max(0.45, Math.Min(0.85, 0.25 + 0.45 * hr)), margin = depth > 2.2 && lod == 0 ? 0.16 : 0.07;
                    double ou, ow, su, sw;
                    SacredParts.Out(s, out ou, out ow);
                    su = s == 0 ? 1 : s == 2 ? -1 : 0;
                    sw = s == 1 ? 1 : s == 3 ? -1 : 0;
                    if (corner == 1)
                    {
                        // The winged corner strut along the diagonal where this side meets the next one.
                        double cu, cv, cw;
                        SacredParts.RoofPoint(r, s, 1, 0, out cu, out cv, out cw);
                        double headV = r.WallTopV + (cv - r.FasciaH - r.WallTopV) * f - margin, dh = hr * 1.41421356;
                        double foot = Math.Max(footV, headV - hr * Tan60 * 1.1), dv = headV - foot, len = Math.Sqrt(dv * dv + dh * dh);
                        if (len < 0.4) continue;
                        MeshData cp = SacredParts.StrutProto(len, size * 1.25, rich, true, Wood, StrutPaint[(seed + 7u) & 3], lod);
                        double fu = su * along + ou * (wall + 0.06), fw = sw * along + ow * (wall + 0.06);
                        Affine3 side = SacredDraw.Turn(k, fu, foot, fw, s * 0.5 * Math.PI - 0.25 * Math.PI);
                        double c2 = dv / len, s2 = dh / len;
                        Shapes.CopyTransformed(m, cp, Affine3.FromBasis(1, 0, 0, 0, c2, s2, 0, -s2, c2, 0, 0, 0).Then(side), 0xFFFFFFFFu);
                        drawn++;
                        continue;
                    }
                    // Mid-side head height (for the prototype's length); each strut is stretched to its own soffit, which
                    // the corner lift raises towards the ends.
                    double headMid = r.WallTopV + (eaveLow - r.WallTopV) * f - margin;
                    double foot0 = Math.Max(footV, headMid - hr * Tan60), dv0 = headMid - foot0, dw = hr - 0.06, len0 = Math.Sqrt(dv0 * dv0 + dw * dw);
                    if (len0 < 0.35) continue;
                    MeshData proto = SacredParts.StrutProto(len0, size, rich, false, Wood, StrutPaint[(seed + (uint)s) & 3], lod);
                    int per = Math.Max(2, (int)Math.Round(2 * along / pitch)), draw = lod == 0 ? per : Math.Max(2, per / 2);
                    for (int q = 0; q < draw; q++)
                    {
                        double x = -along + 2 * along * (q + 0.5) / draw;
                        double eu, ev, ew;
                        SacredParts.RoofPoint(r, s, Math.Max(-1, Math.Min(1, x / eaveAlong)), 0, out eu, out ev, out ew);
                        double headV = r.WallTopV + (ev - r.FasciaH - r.WallTopV) * f - margin;
                        double foot = Math.Max(footV, headV - hr * Tan60), dv = headV - foot, len = Math.Sqrt(dv * dv + dw * dw);
                        if (len < 0.3) continue;
                        double sc = len / len0, cy = dv / len, sy = dw / len;
                        double fu = su * x + ou * (wall + 0.06), fw = sw * x + ow * (wall + 0.06);
                        Affine3 side = SacredDraw.Turn(k, fu, foot, fw, s * 0.5 * Math.PI);
                        Affine3 xf = Affine3.FromBasis(1, 0, 0, 0, cy * sc, sy * sc, 0, -sy, cy, 0, 0, 0).Then(side);
                        uint h = ShapeNoise.Hash(s, q, 17, seed), d = 228u + (h % 28u);
                        Shapes.CopyTransformed(m, proto, xf, (d << 24) | (d << 16) | (d << 8) | 0xFFu);
                        drawn++;
                    }
                }
            }
            return drawn;
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
            // Steep struts (about 60°) close-set at most every metre, their heads under the soffit about 0.55 of the way out
            // (the outer eave rests on the rafters), their feet never below footV.
            int n2 = Math.Max(1, (int)Math.Round((u1 - u0) / Math.Min(1.0, strutPitch)));
            if (lod == 1) n2 = Math.Max(1, n2 / 2);
            double hr = Math.Min(0.55 * over, 1.0), headV = sv + (eaveV - Fh - sv) * (hr / Math.Max(0.1, over - 0.04)) - 0.05;
            double foot = Math.Max(footV, headV - 1.7320508 * hr);
            for (int i = 0; i < n2; i++)
            {
                double u = u0 + (u1 - u0) * (i + 0.5) / n2;
                if (carved) FigureStrut(m, k, u, foot, 0.03, headV, hr, lod);
                else Strut(m, k, u, foot, 0.03, headV, hr, 0.05, i % 2 == 0 ? Wood : WoodMid);
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

        /// <summary>
        /// Kumari Ghar's ridge cluster (c_kumari c_00, kumari ref_00-ref_05): five separate slim gilt pinnacles in a row on a
        /// common gilt base at (u, v, w), gaps between them, the middle one tallest (<paramref name="h"/>) and the outer ones
        /// shorter; each a small square foot, a gilt bell (ghanta), a ring and a long thin spire.
        /// </summary>
        public static void PinnacleCluster(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            Box(m, k, u - 1.45, u + 1.45, v, v + 0.18, w - 0.3, w + 0.3, GiltDeep, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1) Box(m, k, u - 1.35, u + 1.35, v + 0.18, v + 0.26, w - 0.24, w + 0.24, Gilt, BoxFaces.All & ~BoxFaces.Bottom);
            int seg = lod == 0 ? 10 : lod == 1 ? 6 : 4;
            for (int i = -2; i <= 2; i++)
            {
                if (lod >= 2 && Math.Abs(i) == 1) continue;
                double hh = h * (i == 0 ? 1.0 : Math.Abs(i) == 1 ? 0.84 : 0.7), rr = i == 0 ? 0.17 : 0.14, pu = u + 0.62 * i, b0 = v + 0.26;
                Box(m, k, pu - 1.1 * rr, pu + 1.1 * rr, b0, b0 + 0.1, w - 1.1 * rr, w + 1.1 * rr, GiltDeep, BoxFaces.All & ~BoxFaces.Bottom);
                var p = new Profile2(14);
                p.Add(0, 0, true).Add(0.6 * rr, 0, true).Add(rr, 0.08 * hh).Add(rr * 0.95, 0.18 * hh).Add(0.62 * rr, 0.3 * hh).Add(0.42 * rr, 0.34 * hh)
                 .Add(0.6 * rr, 0.38 * hh).Add(0.3 * rr, 0.42 * hh).Add(0.22 * rr, 0.6 * hh).Add(0.32 * rr, 0.64 * hh).Add(0.12 * rr, 0.68 * hh)
                 .Add(0.06 * rr, 0.92 * hh).Add(0, hh, true);
                Shapes.Lathe(m, SacredDraw.At(k, pu, b0 + 0.1, w), Gilt, p, seg);
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
