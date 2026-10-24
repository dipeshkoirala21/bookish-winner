using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// PT-01, the main Pashupatinath temple (1692-1697, Bhupalendra Malla; pashupati-temple.md §4 PT-01) and PT-03 its
    /// gajur and trident, bottom to top, all heights above the courtyard and plans from the OSM parts [O]:
    /// <list type="bullet">
    /// <item>one grey stone plinth, 0.9 m, to the full OSM outline (w913170315, 19.2 × 19.4 m), with a flight of four
    /// steps at each of the four doors and brass lamp stands;</item>
    /// <item>the ground storey, 12.9 × 13.1 m and 5.6 m tall, round the closed sanctum, with the covered walk outside it
    /// under the lower roof: on each face the silver repoussé double door with its gilt frame and semicircular gilt torana,
    /// gilt-framed niches with gold guardian images either side, gilt-framed repoussé panels, the carved frieze of small
    /// heads, upper panels, stone corner quoins and the projecting cornice the struts spring from;</item>
    /// <item>the lower struts (nine a side and a winged one at each corner, carved deities) and between them the slanting
    /// black lattice skirt, under the lower gilt roof (eave 18.3 × 18.5 m at 9.0 m, to 11.5 m, w1032558820) with its
    /// standing seams, hip rolls and curled corner tips;</item>
    /// <item>the upper body (8.3 × 8.7 m, w1032559975) of dark glazed brick and carved wood with small lattice windows,
    /// the upper struts (six a side and the corners) and skirt, and the upper gilt roof (eave 12.9 × 13.3 m at 14.5 m to
    /// a 2.4 m flat top at 19.0 m, w1032559974);</item>
    /// <item>the gajur (19.0 to 23.6 m): stepped gilt base with upturned corner horns, four corner bell pinnacles, the
    /// great bell, the lotus petal ring, the pot, two small bells and a small pot, the stem and the tiered parasol, the
    /// scalloped streamer hanging down the east face and the gilt trident with its damaru on a pole braced to the bell.</item>
    /// </list>
    /// No pataka down the roofs, no eave bells, no outer colonnade and no great stair: the photos show none (the old
    /// parametric hero had all four). The doors are closed (the sanctum is never entered or shown).
    /// </summary>
    internal static class PashupatiTemple
    {
        public const double PlinthM = 0.9;

        /// <summary>Ground storey wall half sizes (along a, along b), its top and the cornice top.</summary>
        public const double WallA = 6.45, WallB = 6.55, WallTopM = 6.45, CorniceTopM = 6.8;

        /// <summary>Lower roof: eave 18.3 × 18.5 m at 9.0 m (OSM w1032558820) rising to 12.4 m where it meets the upper
        /// body; the classic east-bank photos (roof/ref_05, night/ref_05, measured against the eave width and the published
        /// 23.6 m total) give a 30° slope and a 3.4 m rise (the OSM part's 11.5 m top is an estimate).</summary>
        public const double LowerEaveA = 9.15, LowerEaveB = 9.25, LowerEaveM = 9.0, LowerTopM = 12.4;

        /// <summary>Upper body (6.8 × 7.0 m: the photos put it at 0.37 of the lower eave; OSM w1032559975, 8.3 × 8.7 m, is
        /// the sanctum core below) and its cornice, the foot of the upper skirt.</summary>
        public const double UpperA = 3.4, UpperB = 3.5, UpperCorniceM = 13.2;

        /// <summary>Upper roof: eave 11.8 × 12.0 m (0.62 of the lower eave in the photos; OSM w1032559974 maps 12.9 × 13.3)
        /// at 15.4 m to a 2.4 m flat top at 18.3 m (32°).</summary>
        public const double UpperEaveA = 5.9, UpperEaveB = 6.0, UpperEaveM = 15.4, RoofTopM = 18.3, RoofTopHalf = 1.2;

        /// <summary>The gajur's height from its base on the flat top to the tip (5.0 m in the photos, to 23.6 m).</summary>
        public const double GajurM = 5.05;

        /// <summary>Struts per side: lower and upper tiers [P: night/ref_05 counts 9-11 on the east face].</summary>
        public const int LowerStruts = 9, UpperStruts = 6;

        /// <summary>Pitch of the screens' fine perforation grid (j_nandi/ref_04: about 6 cm holes; drawn as lines every 14 cm).</summary>
        public const double ScreenPitchM = 0.14;

        /// <summary>Door opening (silver leaves) and its sill height.</summary>
        public const double DoorW = 1.8, DoorH = 3.2;

        [ThreadStatic] private static double[] _pa, _pb;

        static PashupatiTemple()
        {
            GajurProfile = BuildGajurProfile(out GajurCreaseMask);
        }

        public static void Build(ref PashupatiFrame f, MeshData m, SacredStats stats)
        {
            int lod = f.Lod;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            if (lod >= 3)
            {
                Far(ref f, m);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            Plinth(ref f, m, k, lod);
            for (int s = 0; s < 4; s++) Face(ref f, m, s, lod);
            // The ground storey's cornice (two projecting steps, gilt edge) where the lower struts spring from.
            PashupatiKit.Band(m, k, 0, 0, WallA, WallB,
                              new[] { 0.0, WallTopM, 0.16, WallTopM + 0.04, 0.16, WallTopM + 0.16, 0.3, WallTopM + 0.2, 0.3, CorniceTopM }, PashupatiKit.WoodDark,
                              true, PashupatiKit.WoodDark);
            if (lod <= 1)
                PashupatiKit.Band(m, k, 0, 0, WallA, WallB, new[] { 0.31, WallTopM + 0.2, 0.33, WallTopM + 0.24, 0.33, WallTopM + 0.3, 0.31, WallTopM + 0.33 },
                                  PashupatiKit.GiltEave, false, PashupatiKit.GiltEave);

            // Lower tier: skirt, struts, roof.
            double fascia = 0.32, skirtWU = WallA + 0.3, skirtWW = WallB + 0.3;
            PashupatiKit.Skirt(m, k, skirtWU, skirtWW, CorniceTopM, LowerEaveA - 0.06, LowerEaveB - 0.06, LowerEaveM - fascia, ScreenPitchM, lod);
            int struts = PashupatiKit.Struts(m, k, WallA, WallB, CorniceTopM - 0.12, LowerEaveA, LowerEaveB, LowerEaveM - fascia, LowerStruts, LowerStruts, 0.86, 101u,
                                             lod);
            if (lod == 2)
                PashupatiKit.FarStruts(m, k, skirtWU, skirtWW, CorniceTopM, LowerEaveA - 0.06, LowerEaveB - 0.06, LowerEaveM - fascia, LowerStruts);
            var lower = new PRoof
            {
                EaveU = LowerEaveA, EaveW = LowerEaveB, EaveV = LowerEaveM, TopU = UpperA + 0.12, TopW = UpperB + 0.12, TopV = LowerTopM, Lift = 0.22,
                FasciaH = fascia, SeamPitch = 0.84, CurlSize = 0.55, Roof = PashupatiKit.GiltRoof, Seam = PashupatiKit.GiltRib, Fascia = PashupatiKit.GiltEave,
                Curl = PashupatiKit.GiltBright,
            };
            PashupatiKit.Roof(m, k, lower, lod);

            // Upper body: dark glazed brick between carved wooden bands, small lattice windows, the gilt flashing where the
            // lower roof meets it, the upper cornice.
            UpperBody(ref f, m, k, lod);
            double ufascia = 0.28;
            PashupatiKit.Skirt(m, k, UpperA + 0.25, UpperB + 0.25, UpperCorniceM, UpperEaveA - 0.05, UpperEaveB - 0.05, UpperEaveM - ufascia, ScreenPitchM, lod);
            struts += PashupatiKit.Struts(m, k, UpperA, UpperB, UpperCorniceM - 0.1, UpperEaveA, UpperEaveB, UpperEaveM - ufascia, UpperStruts, UpperStruts, 0.84,
                                          203u, lod, true);
            if (lod == 2)
                PashupatiKit.FarStruts(m, k, UpperA + 0.25, UpperB + 0.25, UpperCorniceM, UpperEaveA - 0.05, UpperEaveB - 0.05, UpperEaveM - ufascia, UpperStruts);
            var upper = new PRoof
            {
                EaveU = UpperEaveA, EaveW = UpperEaveB, EaveV = UpperEaveM, TopU = RoofTopHalf, TopW = RoofTopHalf, TopV = RoofTopM, Lift = 0.16,
                FasciaH = ufascia, SeamPitch = 0.8, CurlSize = 0.45, Roof = PashupatiKit.GiltRoof, Seam = PashupatiKit.GiltRib, Fascia = PashupatiKit.GiltEave,
                Curl = PashupatiKit.GiltBright,
            };
            PashupatiKit.Roof(m, k, upper, lod);
            // The flat top the gajur stands on.
            PashupatiKit.Band(m, k, 0, 0, RoofTopHalf, RoofTopHalf, new[] { -0.02, RoofTopM - 0.05, 0.04, RoofTopM, 0.04, RoofTopM + 0.1 }, PashupatiKit.GiltEave, true,
                              PashupatiKit.GiltRoof);
            Gajur(m, k, RoofTopM + 0.1, PashupatiSite.TempleTopM - RoofTopM - 0.1, lod);
            if (stats != null) stats.Struts = struts;

            // Colliders: the walkable plinth band round the closed walls (gaps where the flights come up), the walls
            // (never climbed) and the roofs above them.
            f.Box(-WallA, WallA, -WallB, WallB, PlinthM, LowerEaveM, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
            PlinthColliders(ref f);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Plinth and steps
        // ---------------------------------------------------------------------------------------------------------

        private static void Plinth(ref PashupatiFrame f, MeshData m, in Affine3 k, int lod)
        {
            if (_pa == null)
            {
                _pa = new double[64];
                _pb = new double[64];
            }
            // The plinth is the OSM outline itself (V4: within 0.1 m) when the tile has it, else its measured rectangle.
            int n = f.Outline(913170315UL, _pa, _pb);
            if (n < 3) n = SacredDraw.Rect(0, 0, 9.6, 9.7, _pa, _pb);
            Mould p = SacredDraw.M;
            if (lod >= 2)
            {
                p.Add(0, -0.3, PashupatiKit.StonePlinth).Add(0, PlinthM, PashupatiKit.StoneEdge);
            }
            else
            {
                // A proud base course, the stone face, and the moulded top edge.
                p.Add(0.06, -0.3, PashupatiKit.StoneDark).Add(0.06, 0.12).Add(0, 0.16, PashupatiKit.StonePlinth).Add(0, PlinthM - 0.2)
                 .Add(0.05, PlinthM - 0.16, PashupatiKit.StoneEdge).Add(0.05, PlinthM - 0.08).Add(0.09, PlinthM - 0.04).Add(0.09, PlinthM);
            }
            SacredDraw.Ring(m, k, _pa, _pb, n, p);
            double o = p.O[p.Count - 1];
            SacredDraw.Cap(m, k, _pa, _pb, n, o, PlinthM, true, PashupatiKit.Br(0x9A958A, MaterialChannel.Flagstone));
            for (int s = 0; s < 4; s++) Steps(ref f, m, s, lod);
        }

        /// <summary>The flight of four steps up to the plinth at the door of side s (sides as <see cref="PashupatiKit.Out"/>:
        /// 0 = north +b, 1 = west −a (main), 2 = south, 3 = east), 3 m wide, projecting 1.1 m.</summary>
        private static void Steps(ref PashupatiFrame f, MeshData m, int s, int lod)
        {
            double du, dw;
            PashupatiKit.Out(s, out du, out dw);
            double edge = s % 2 == 0 ? 9.7 : 9.6;
            Affine3 face = f.Facing(du * edge, 0, dw * edge, du, dw);
            const int N = 4;
            const double Tread = 0.28, Half = 1.5;
            double rise = PlinthM / N;
            ShapeBrush tread = PashupatiKit.Br(0x9A958A, MaterialChannel.Stone), riser = PashupatiKit.Ao(PashupatiKit.StonePlinth, 0.85f);
            for (int i = 1; i <= N; i++)
            {
                // Step i counts from the top (i = 1 against the plinth edge) and stands on the floor.
                double top = PlinthM - (i - 1) * rise, z0 = (i - 1) * Tread, z1 = i * Tread;
                if (i == 1) continue; // the plinth's own top is the first tread
                SacredDraw.Box(m, face, -Half, Half, 0, top, z0 - 0.02, z1, tread, BoxFaces.Top);
                SacredDraw.Box(m, face, -Half, Half, 0, top, z0, z1, riser, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            if (lod <= 1)
            {
                // Side cheeks (low stone walls) and, at LOD0, brass oil-lamp stands on them.
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    SacredDraw.Box(m, face, sg * Half, sg * (Half + 0.32), 0, PlinthM + 0.25, -0.02, (N - 1) * Tread + 0.05, PashupatiKit.StoneEdge,
                                   BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
                    if (lod == 0)
                    {
                        PashupatiKit.LatheOf(m, SacredDraw.At(face, sg * (Half + 0.16), PlinthM + 0.25, 0.4), PashupatiKit.Brass,
                                             new[] { 0.0, 0.0, 0.1, 0.0, 0.1, 0.03, 0.03, 0.08, 0.025, 0.55, 0.06, 0.6, 0.14, 0.66, 0.15, 0.7, 0.0, 0.7 }, 10, lod, 0b1_0000_0010);
                    }
                }
            }
        }

        private static void PlinthColliders(ref PashupatiFrame f)
        {
            // Inner block under the walls and the walk, then four side strips split at their flights (gaps 3 m wide).
            const double Inner = 8.4, Gap = 1.55;
            f.Box(-Inner, Inner, -Inner, Inner, -0.3, PlinthM, GenColliderFlags.Walkable, GenColliders.Stone);
            for (int s = 0; s < 4; s++)
            {
                double du, dw;
                PashupatiKit.Out(s, out du, out dw);
                double edge = s % 2 == 0 ? 9.7 : 9.6, along = s % 2 == 0 ? 9.6 : 9.7;
                if (du == 0)
                {
                    double b0 = dw > 0 ? Inner : -edge, b1 = dw > 0 ? edge : -Inner;
                    f.Box(-along, -Gap, b0, b1, -0.3, PlinthM, GenColliderFlags.Walkable, GenColliders.Stone);
                    f.Box(Gap, along, b0, b1, -0.3, PlinthM, GenColliderFlags.Walkable, GenColliders.Stone);
                    f.Box(-Gap, Gap, b0, b1, -0.3, 0.05, GenColliderFlags.Walkable, GenColliders.Stone);
                    f.Ramp(0, dw * (edge + 1.1), 0, 0, dw * Inner, PlinthM, Gap, GenColliders.Stone);
                }
                else
                {
                    double a0 = du > 0 ? Inner : -edge, a1 = du > 0 ? edge : -Inner;
                    f.Box(a0, a1, -along, -Gap, -0.3, PlinthM, GenColliderFlags.Walkable, GenColliders.Stone);
                    f.Box(a0, a1, Gap, along, -0.3, PlinthM, GenColliderFlags.Walkable, GenColliders.Stone);
                    f.Ramp(du * (edge + 1.1), 0, 0, du * Inner, 0, PlinthM, Gap, GenColliders.Stone);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // The ground storey's faces
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// One face of the ground storey (c_inner/c_01, c_westgate/c_26, c_gje/c_03), bottom to top: the proud base
        /// course, pale ashlar between the panels and at the corner piers with their quoins; the door in its three silver
        /// frame bands under the flat silver torana; the gilt-framed niches with their gold guardians either side; the
        /// tall gilt-framed repoussé panels of four-armed deities; one continuous gilt-framed band of small seated deities
        /// each side of the torana; and directly under the cornice the dark frieze of large carved heads between gilt
        /// lines. LOD2: the panels as dark fields in thin gilt rims, the torana a dark half disc, the frieze a dark band.
        /// </summary>
        private static void Face(ref PashupatiFrame f, MeshData m, int s, int lod)
        {
            double du, dw;
            PashupatiKit.Out(s, out du, out dw);
            double off = s % 2 == 0 ? WallB : WallA, half = s % 2 == 0 ? WallA : WallB;
            Affine3 x = f.Facing(du * off, 0, dw * off, du, dw);
            double y0 = PlinthM;
            // The wall field: pale ashlar with a proud base course (one quad strip per face; corners closed by the quoins).
            SacredDraw.Box(m, x, -half, half, y0, WallTopM, -0.4, 0, WallField, BoxFaces.Front);
            SacredDraw.Box(m, x, -half - 0.06, half + 0.06, y0, y0 + 0.42, -0.4, 0.07, PashupatiKit.StoneWall, BoxFaces.Front | BoxFaces.Top);
            // Corner piers' quoins (they also close the corners of the wall boxes).
            for (int sg = -1; sg <= 1; sg += 2)
                SacredDraw.Box(m, x, sg > 0 ? half - 0.36 : -half, sg > 0 ? half : -half + 0.36, y0, WallTopM, -0.4, 0.05, Quoin,
                               BoxFaces.Front | (sg > 0 ? BoxFaces.Right : BoxFaces.Left));
            Door(m, x, y0, lod);
            double band0 = BandBottomM, band1 = BandTopM, inner = 1.5, outer = half - 0.4;
            if (lod >= 2)
            {
                // Far: dark repoussé fields in thin gilt rims, the band and the heads frieze as dark strips.
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    FarPanel(m, x, sg * 3.3, PanelBottomM, 1.3, PanelTopM - PanelBottomM);
                    FarPanel(m, x, sg * (half - 1.25), PanelBottomM, 1.4, PanelTopM - PanelBottomM);
                    SacredDraw.Box(m, x, sg > 0 ? inner : -outer, sg > 0 ? outer : -inner, band0, band1, 0, 0.05, PanelField, BoxFaces.Front);
                }
                SacredDraw.Box(m, x, -half + 0.36, half - 0.36, HeadsBottomM, WallTopM, 0, 0.1, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Bottom);
                return;
            }
            for (int sg = -1; sg <= 1; sg += 2)
            {
                // The niche with its gold guardian beside the door.
                Niche(m, x, sg * 2.05, y0 + 0.55, 0.95, 2.15, lod, sg);
                // Two tall gilt-framed repoussé panels each side.
                Panel(m, x, sg * 3.35, PanelBottomM, 1.3, PanelTopM - PanelBottomM, lod);
                Panel(m, x, sg * (half - 1.22), PanelBottomM, 1.45, PanelTopM - PanelBottomM, lod);
                // The continuous gilt-framed band of small seated deities from the torana to the corner pier.
                Band(m, x, sg > 0 ? inner : -outer, sg > 0 ? outer : -inner, band0, band1, lod);
            }
            // Directly under the cornice: the dark frieze of large carved heads between gilt lines.
            SacredDraw.Box(m, x, -half + 0.36, half - 0.36, HeadsBottomM, WallTopM, 0, 0.1, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            SacredDraw.Box(m, x, -half + 0.36, half - 0.36, HeadsBottomM - 0.06, HeadsBottomM, 0, 0.13, PashupatiKit.GiltRib,
                           BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            if (lod == 0)
            {
                const double Pitch = 0.42;
                int heads = (int)Math.Floor((2 * half - 0.9) / Pitch);
                double start = -0.5 * heads * Pitch;
                for (int q = 0; q < heads; q++)
                {
                    double u = start + Pitch * (q + 0.5);
                    // A carved head (0.3 m) on the dark beam.
                    Shapes.Ellipsoid(m, SacredDraw.At(x, u, HeadsBottomM + 0.21, 0.12), HeadStone, 0.12, 0.155, 0.09, 4, false);
                }
            }
        }

        /// <summary>Heights above the courtyard of the face's bands: the tall panels, the seated-figure band, the heads frieze.</summary>
        private const double PanelBottomM = 1.55, PanelTopM = 4.75, BandBottomM = 5.0, BandTopM = 5.86, HeadsBottomM = 6.0;

        /// <summary>The ground storey reads dark (c_gje/c_03 median #434245, night/ref_05): charcoal carved wood between the
        /// blackened bronze panels, pale stone only at the base course and the quoins.</summary>
        private static readonly ShapeBrush WallField = PashupatiKit.Br(0x37322E, MaterialChannel.WoodCarved);
        private static readonly ShapeBrush Quoin = PashupatiKit.Br(0x46413C, MaterialChannel.Stone);
        private static readonly ShapeBrush HeadStone = PashupatiKit.Br(0x4E4338, MaterialChannel.Metal);
        private static readonly ShapeBrush SilverFrameIn = PashupatiKit.Br(0x8C8A84, MaterialChannel.Metal);
        private static readonly ShapeBrush SilverFrameMid = PashupatiKit.Br(0xA6A49C, MaterialChannel.Metal);

        /// <summary>The silver repoussé doors: the leaf's field and its raised panels, roundels and bosses (night/ref_05,
        /// nandi/ref_03), the lattice grooves of the middle panels.</summary>
        private static readonly ShapeBrush SilverLeaf = PashupatiKit.Br(0x8E9094, MaterialChannel.Metal);
        private static readonly ShapeBrush SilverRaised = PashupatiKit.Br(0xB9BCC0, MaterialChannel.Metal);
        private static readonly ShapeBrush SilverGroove = PashupatiKit.Br(0x74767A, MaterialChannel.Metal);

        /// <summary>The door torana: gilt relief on a darker gilt field (glowing gilt at night in night/ref_05).</summary>
        private static readonly ShapeBrush ToranaField = PashupatiKit.Br(0xA88C52, MaterialChannel.Gilt);
        private static readonly ShapeBrush ToranaRelief = PashupatiKit.GiltBright;
        private static readonly ShapeBrush ToranaDark = PashupatiKit.Br(0x6E5A34, MaterialChannel.Gilt);

        /// <summary>The wall panels' blackened bronze figures and the thin gilt of their crowns, aureoles and frame lines.</summary>
        private static readonly ShapeBrush Bronze = PashupatiKit.Br(0x5C4A34, MaterialChannel.Metal);
        private static readonly ShapeBrush GiltLine = PashupatiKit.Br(0xC9A24A, MaterialChannel.Gilt);

        /// <summary>
        /// The closed door (night/ref_05, nandi/ref_03, c_westgate/c_26): two leaves of silver repoussé, each with raised
        /// silver panels on its darker field (a sun or a moon roundel at the top, a tall lattice panel with its grooves, an
        /// oval medallion, a boss at the foot), the split between the leaves, three nested silver frame bands of 15 cm
        /// stepping out with a gilt edge, two small bronze guardians at the foot of the frame, the stone sill, and over it
        /// the gilt torana. LOD1: the leaves and their raised panels flat (no voids); LOD2: the leaves and the outer band.
        /// </summary>
        private static void Door(MeshData m, in Affine3 x, double y0, int lod)
        {
            double hw = 0.5 * DoorW, top = y0 + DoorH;
            SacredDraw.Panel(m, x, -hw, y0, hw, top, 0.01, SilverLeaf);
            if (lod <= 1)
            {
                BoxFaces raised = lod == 0 ? BoxFaces.Front | BoxFaces.Wall : BoxFaces.Front;
                SacredDraw.Box(m, x, -0.025, 0.025, y0, top, 0.01, 0.05, SilverFrameIn, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    double c = sg * 0.45;
                    // The tall middle panel, the lower panel and the roundel's square, raised 3 cm.
                    SacredDraw.Box(m, x, c - 0.33, c + 0.33, y0 + 1.0, y0 + 2.25, 0.01, 0.04, SilverRaised, raised);
                    SacredDraw.Box(m, x, c - 0.33, c + 0.33, y0 + 0.2, y0 + 0.85, 0.01, 0.04, SilverRaised, raised);
                    SacredDraw.Box(m, x, c - 0.33, c + 0.33, y0 + 2.4, y0 + 3.08, 0.01, 0.035, SilverRaised, raised);
                    if (lod > 0) continue;
                    // Lattice grooves across the middle panel.
                    for (int q = 1; q <= 4; q++)
                    {
                        double gy = y0 + 1.0 + 1.25 * q / 5;
                        SacredDraw.Box(m, x, c - 0.3, c + 0.3, gy - 0.012, gy + 0.012, 0.04, 0.046, SilverGroove, BoxFaces.Front);
                    }
                    for (int q = -1; q <= 1; q += 2)
                        SacredDraw.Box(m, x, c + q * 0.11 - 0.012, c + q * 0.11 + 0.012, y0 + 1.03, y0 + 2.22, 0.04, 0.046, SilverGroove, BoxFaces.Front);
                    // The sun (left leaf) and the moon (right leaf) roundels at the top, the oval medallion, the foot boss.
                    Affine3 rd = Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, c, y0 + 2.74, 0.035));
                    Shapes.Cylinder(m, rd, SilverFrameMid, 0.27, 0.03, 12, 0.01, 1, false, true);
                    Shapes.Ellipsoid(m, SacredDraw.At(x, c, y0 + 2.74, 0.07), SilverRaised, 0.16, 0.16, 0.035, 6, false);
                    if (sg < 0)
                        for (int r = 0; r < 8; r++)
                        {
                            double a = 2 * Math.PI * r / 8;
                            Shapes.Bar(m, x, SilverRaised, c + 0.18 * Math.Cos(a), y0 + 2.74 + 0.18 * Math.Sin(a), 0.07, c + 0.25 * Math.Cos(a),
                                       y0 + 2.74 + 0.25 * Math.Sin(a), 0.07, 0.016, 3);
                        }
                    else
                        Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, c + 0.02, y0 + 2.74, 0.07)), SilverFrameIn, 0.2, 0.025, 12, 3, default,
                                     120, 200, true);
                    Shapes.Ellipsoid(m, SacredDraw.At(x, c, y0 + 0.52, 0.05), SilverFrameMid, 0.2, 0.22, 0.03, 6, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(x, c, y0 + 0.52, 0.07), SilverRaised, 0.08, 0.09, 0.03, 6, false);
                }
            }
            // Three nested silver frame bands of 15 cm, each standing 4 cm prouder, the outer one edged in gilt.
            BoxFaces wall = lod >= 2 ? BoxFaces.Front : BoxFaces.Wall | BoxFaces.Top;
            for (int b = 0; b < 3; b++)
            {
                double i0 = hw + 0.15 * b, i1 = i0 + 0.15, t0 = top + 0.15 * b, t1 = t0 + 0.15, z = 0.06 + 0.04 * b;
                ShapeBrush br = b == 1 ? SilverFrameMid : SilverFrameIn;
                if (lod >= 2 && b < 2) continue;
                SacredDraw.Box(m, x, -i1, -i0, y0, t1, 0, z, br, wall);
                SacredDraw.Box(m, x, i0, i1, y0, t1, 0, z, br, wall);
                SacredDraw.Box(m, x, -i0, i0, t0, t1, 0, z, br, wall | BoxFaces.Bottom);
            }
            double fo = hw + 0.45, ft = top + 0.45;
            if (lod <= 1)
            {
                SacredDraw.Box(m, x, -fo - 0.03, -fo, y0, ft + 0.03, 0, 0.15, GiltLine, BoxFaces.Front | BoxFaces.Left);
                SacredDraw.Box(m, x, fo, fo + 0.03, y0, ft + 0.03, 0, 0.15, GiltLine, BoxFaces.Front | BoxFaces.Right);
                SacredDraw.Box(m, x, -fo, fo, ft, ft + 0.03, 0, 0.15, GiltLine, BoxFaces.Front | BoxFaces.Top);
            }
            SacredDraw.Box(m, x, -fo - 0.1, fo + 0.1, y0 - 0.02, y0 + 0.12, 0, 0.22, PashupatiKit.StoneEdge, wall);
            // The two small guardians at the foot of the frame.
            if (lod == 0)
                for (int sg = -1; sg <= 1; sg += 2)
                    PashupatiFigures.Deity(m, x, sg * (hw + 0.225), y0 + 0.14, 0.14, 0.62, Bronze, GiltLine, 1);
            Torana(m, x, 0, ft + 0.02, 0.05, ToranaWidthM, lod);
        }

        /// <summary>The door torana's width (2.6 m) over its frame.</summary>
        private const double ToranaWidthM = 2.6;

        /// <summary>
        /// A flat semicircular silver repoussé torana (c_westgate/c_26, j_nandi/ref_04, c_inner/c_06) of width w standing
        /// on (u, v) on face frame x: the slightly pointed half disc, its gilt outer rim and a thinner inner rim, the seated
        /// deity in relief in the middle, the kirtimukha mask at the apex and two coiled scrolls at its feet. LOD1: the disc
        /// and the rim; LOD2: a dark half disc.
        /// </summary>
        private static void Torana(MeshData m, in Affine3 x, double u, double v, double z, double w, int lod)
        {
            double r = 0.5 * w, h = 0.86 * r;
            int seg = lod == 0 ? 12 : lod == 1 ? 8 : 6;
            double[] px = PashupatiKit.ScratchU, pz = PashupatiKit.ScratchW;
            int n = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg;
                px[n] = r * Math.Cos(a);
                pz[n] = -(h * Math.Sin(a) + 0.1 * h * Math.Pow(Math.Sin(a), 10));
                n++;
            }
            Affine3 b = SacredDraw.Basis(x, u, v, z, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, b, lod >= 2 ? ToranaDark : ToranaField, px, pz, n, 0.05, 0, 0, BevelStyle.Chamfer, true, false);
            if (lod >= 2) return;
            // Gilt outer rim along the arc (an elliptical half torus following the pointed disc).
            Affine3 rim = Affine3.Scaling(1, h * 1.04 / r, 1).Then(Affine3.RotationX(0.5 * Math.PI)).Then(SacredDraw.At(x, u, v, z + 0.05));
            Shapes.Torus(m, rim, ToranaRelief, r, 0.045, lod == 0 ? 16 : 10, 4, default, 90, 180, true);
            if (lod > 0) return;
            Affine3 rim2 = Affine3.Scaling(1, h * 0.98 / r, 1).Then(Affine3.RotationX(0.5 * Math.PI)).Then(SacredDraw.At(x, u, v, z + 0.05));
            Shapes.Torus(m, rim2, ToranaRelief, 0.72 * r, 0.022, 12, 3, default, 90, 180, true);
            // The central deity boss, the kirtimukha and the scrolls, in bright gilt relief.
            PashupatiFigures.SeatedSmall(m, x, u, v + 0.03, z + 0.05, 0.62 * h, ToranaRelief, ToranaRelief, 0);
            PashupatiFigures.Relief(m, x, FigureKind.Kirtimukha, u, v + 0.84 * h, z + 0.05, 0.3 * h, 0.2 * r, 0.05, ToranaRelief, 0);
            for (int sg = -1; sg <= 1; sg += 2)
                Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, u + sg * 0.8 * r, v + 0.14 * h, z + 0.06)), ToranaRelief, 0.1 * r, 0.025, 10, 3,
                             default, sg > 0 ? 0 : 90, 270, true);
        }

        /// <summary>A gilt-framed niche at (u, v) of a face (width w, height h): a dark recess and a gold guardian image
        /// (a standing figure with a crown and an aureole) on a small lotus base; the frame arched at the top.</summary>
        private static void Niche(MeshData m, in Affine3 x, double u, double v, double w, double h, int lod, int side)
        {
            double hw = 0.5 * w;
            SacredDraw.Panel(m, x, u - hw, v, u + hw, v + h, 0.012, PashupatiKit.Ink);
            BoxFaces f = BoxFaces.Front | BoxFaces.Wall;
            SacredDraw.Box(m, x, u - hw - 0.1, u - hw, v, v + h, 0, 0.1, GiltLine, f);
            SacredDraw.Box(m, x, u + hw, u + hw + 0.1, v, v + h, 0, 0.1, GiltLine, f);
            SacredDraw.Box(m, x, u - hw - 0.14, u + hw + 0.14, v - 0.1, v, 0, 0.14, GiltLine, f);
            // The cusped arch over the niche (a small torana).
            SacredParts.Torana(m, x, u, v + h - 0.02, 0.02, w + 0.2, GiltLine, Math.Max(1, lod));
            // The guardian image (gilt, about 1.5 m) in the round (c_gje/c_03, nandi/ref_03).
            PashupatiFigures.Guardian(m, x, u, v, 0.18, 1.55, side, PashupatiKit.GiltBright, PashupatiKit.GiltShade, lod);
        }

        private static readonly ShapeBrush PanelField = PashupatiKit.Br(0x2E2A27, MaterialChannel.Metal);

        /// <summary>A gilt-framed panel of blackened repoussé centred at u from v (width w, height h; c_inner/c_01,
        /// c_gje/c_03): the dark field in thin gilt frame lines and on it a crowned four-armed deity in modelled dark bronze
        /// relief on a lotus, its crown and aureole gilt. LOD1: the coarse figure.</summary>
        private static void Panel(MeshData m, in Affine3 x, double u, double v, double w, double h, int lod)
        {
            double hw = 0.5 * w, fw = 0.06;
            SacredDraw.Box(m, x, u - hw, u + hw, v, v + h, 0, 0.03, PanelField, BoxFaces.Front);
            BoxFaces f = lod >= 1 ? BoxFaces.Front : BoxFaces.Front | BoxFaces.Wall;
            SacredDraw.Box(m, x, u - hw - fw, u - hw, v - fw, v + h + fw, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u + hw, u + hw + fw, v - fw, v + h + fw, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u - hw, u + hw, v - fw, v, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u - hw, u + hw, v + h, v + h + fw, 0, 0.06, GiltLine, f);
            double fh = 0.86 * h;
            PashupatiFigures.Deity(m, x, u, v + 0.04 * h, 0.03, fh, Bronze, GiltLine, lod);
        }

        /// <summary>The far (LOD2) panel: a dark field in a thin gilt rim.</summary>
        private static void FarPanel(MeshData m, in Affine3 x, double u, double v, double w, double h)
        {
            double hw = 0.5 * w;
            SacredDraw.Box(m, x, u - hw - 0.06, u + hw + 0.06, v - 0.06, v + h + 0.06, 0, 0.02, GiltLine, BoxFaces.Front);
            SacredDraw.Box(m, x, u - hw, u + hw, v, v + h, 0, 0.03, PanelField, BoxFaces.Front);
        }

        /// <summary>The continuous gilt-framed band of small seated deities between u0 and u1 (c_inner/c_01): a dark
        /// repoussé field in a gilt frame holding three seated figures, each with its own small gilt arch.</summary>
        private static void Band(MeshData m, in Affine3 x, double u0, double u1, double v0, double v1, int lod)
        {
            const double Fw = 0.05;
            SacredDraw.Box(m, x, u0, u1, v0, v1, 0, 0.03, PanelField, BoxFaces.Front);
            BoxFaces f = lod >= 1 ? BoxFaces.Front : BoxFaces.Front | BoxFaces.Wall;
            SacredDraw.Box(m, x, u0 - Fw, u0, v0 - Fw, v1 + Fw, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u1, u1 + Fw, v0 - Fw, v1 + Fw, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u0, u1, v0 - Fw, v0, 0, 0.06, GiltLine, f);
            SacredDraw.Box(m, x, u0, u1, v1, v1 + Fw, 0, 0.06, GiltLine, f);
            if (lod > 0) return;
            const int N = 3;
            double pitch = (u1 - u0) / N, fh = 0.8 * (v1 - v0);
            for (int q = 0; q < N; q++)
            {
                double c = u0 + pitch * (q + 0.5);
                PashupatiFigures.SeatedSmall(m, x, c, v0 + 0.06, 0.03, fh, Bronze, GiltLine, 0);
                // The small gilt arch over each figure.
                Shapes.Torus(m, Affine3.Scaling(1, 1.1, 1).Then(Affine3.RotationX(0.5 * Math.PI)).Then(SacredDraw.At(x, c, v0 + 0.5 * fh, 0.04)), GiltLine,
                             0.33, 0.02, 10, 3, default, 90, 180, true);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Upper body
        // ---------------------------------------------------------------------------------------------------------

        private static void UpperBody(ref PashupatiFrame f, MeshData m, in Affine3 k, int lod)
        {
            double v0 = LowerTopM - 0.35;
            // The wall: dark glazed brick (dachi apa) under a carved wooden band.
            Mould p = SacredDraw.M;
            p.Add(0, v0, PashupatiKit.Br(0x5A3026, MaterialChannel.BrickGlazed)).Add(0, LowerTopM + 0.02, PashupatiKit.WoodDark).Add(0, UpperCorniceM - 0.22)
             .Add(0.12, UpperCorniceM - 0.18).Add(0.12, UpperCorniceM - 0.08, PashupatiKit.GiltEave).Add(0.2, UpperCorniceM);
            SacredDraw.RectRing(m, k, 0, 0, UpperA, UpperB, p, true, PashupatiKit.WoodDark);
            // Gilt flashing where the lower roof meets the wall.
            PashupatiKit.Band(m, k, 0, 0, UpperA, UpperB, new[] { 0.16, LowerTopM - 0.08, 0.16, LowerTopM + 0.08, 0.0, LowerTopM + 0.14 }, PashupatiKit.GiltEave, false,
                              PashupatiKit.GiltEave);
            if (lod >= 2) return;
            for (int s = 0; s < 4; s++)
            {
                double du, dw;
                PashupatiKit.Out(s, out du, out dw);
                double off = s % 2 == 0 ? UpperB : UpperA, half = s % 2 == 0 ? UpperA : UpperB;
                Affine3 x = f.Facing(du * off, 0, dw * off, du, dw);
                for (int q = -1; q <= 1; q++)
                    SacredParts.LatticeWindow(m, x, q * 0.62 * half, LowerTopM + 0.36, 0, 0.62, 0.3, PashupatiKit.WoodDark, lod, 3);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gajur (PT-03)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Profile of the gajur above its stepped base as (radius, height) pairs in metres (c_area/c_26, c_27,
        /// c_28): the great bell, 1.3 m wide and 1.5 m tall, with its strong flare and lipped rim and its round shoulder; the
        /// 15 cm lotus disc; the round pot (0.5 m across), a disc and the second pot (0.35 m); the short stem with its disc;
        /// and the tiered conical parasol, 1.85 m tall from a 0.48 m base in six stepped rings, to the tip.</summary>
        private static readonly double[] GajurProfile;

        /// <summary>Creases of <see cref="GajurProfile"/> (one bit per point: hard rings at discs, rims and the parasol's
        /// tiers).</summary>
        private static readonly bool[] GajurCreaseMask;

        /// <summary>The height of the gajur's profile above its base (to the tip).</summary>
        private const double GajurProfileTop = 4.7, GajurBaseTop = 0.5;

        private static double[] BuildGajurProfile(out bool[] crease)
        {
            var p = new System.Collections.Generic.List<double>();
            var c = new System.Collections.Generic.List<bool>();
            void Add(double r, double h, bool hard)
            {
                p.Add(r);
                p.Add(h);
                c.Add(hard);
            }
            // The great bell (c_area/c_26, c_28, pagoda/ref_01): a stout, convex-shouldered bell 1.24 m across at mid-height
            // (0.55 of the 2.24 m base) over its rolled lip.
            Add(0, 0, true);
            Add(0.66, 0, true);
            Add(0.685, 0.05, false);
            Add(0.64, 0.12, true);
            Add(0.62, 0.25, false);
            Add(0.615, 0.6, false);
            Add(0.62, 0.9, false);
            Add(0.585, 1.06, false);
            Add(0.5, 1.2, false);
            Add(0.35, 1.32, false);
            Add(0.18, 1.4, true);
            // The lotus disc.
            Add(0.36, 1.43, true);
            Add(0.385, 1.49, false);
            Add(0.32, 1.56, true);
            // Two heavy knobs (pots), about 0.58 m across, and a small third.
            Add(0.16, 1.6, true);
            Add(0.26, 1.66, false);
            Add(0.3, 1.78, false);
            Add(0.29, 1.9, false);
            Add(0.21, 1.98, true);
            Add(0.25, 2.03, false);
            Add(0.29, 2.13, false);
            Add(0.28, 2.24, false);
            Add(0.2, 2.31, true);
            Add(0.23, 2.34, false);
            Add(0.245, 2.39, false);
            Add(0.17, 2.45, true);
            // The short neck and its disc.
            Add(0.09, 2.48, false);
            Add(0.08, 2.7, true);
            Add(0.13, 2.72, true);
            Add(0.13, 2.77, true);
            Add(0.08, 2.79, true);
            // The tiered parasol: six stepped rings from a 0.48 m base.
            for (int i = 0; i < 6; i++)
            {
                double y = 2.85 + 0.25 * i, r = 0.24 - 0.034 * i;
                Add(r, y, true);
                Add(r, y + 0.045, true);
                Add(0.62 * r, y + 0.07, true);
            }
            Add(0.03, 4.4, false);
            Add(0.014, 4.6, false);
            Add(0, GajurProfileTop, true);
            crease = c.ToArray();
            return p.ToArray();
        }

        /// <summary>A corner pinnacle (lotus collar, bell, two knobs, spike) as (radius, height) pairs.</summary>
        private static readonly double[] PinnacleProfile =
        {
            // A bell on a lotus collar (c_area/c_27, c_area/c_28: the corner bells are inverted cups with a flared rim, about
            // a third of the great bell's height, with a disc, two knobs and a short spike on top).
            0, 0, 0.24, 0, 0.3, 0.04, 0.3, 0.08, 0.22, 0.11, 0.33, 0.13, 0.31, 0.17, 0.28, 0.22, 0.27, 0.34, 0.24, 0.44, 0.17, 0.51, 0.08, 0.54,
            0.11, 0.56, 0.11, 0.6, 0.06, 0.62, 0.09, 0.67, 0.08, 0.73, 0.04, 0.76, 0.06, 0.79, 0.05, 0.84, 0.02, 0.87, 0.012, 0.98, 0, 1.0,
        };

        /// <summary>The far gajur (LOD2: 8 sides; LOD3: 6): the bell, the pots and the parasol cone as one lathe, so the
        /// silhouette stays a bell and a cone instead of a spike.</summary>
        private static readonly double[] GajurFar2 =
        {
            0.0, 0.0, 0.66, 0.0, 0.62, 0.25, 0.62, 0.9, 0.5, 1.2, 0.3, 1.36, 0.36, 1.5, 0.3, 1.82, 0.21, 1.98, 0.29, 2.15, 0.2, 2.31, 0.08, 2.5, 0.24, 2.85, 0.06, 4.25,
            0.0, GajurProfileTop,
        };

        private static readonly double[] GajurFar3 = { 0.66, 0.0, 0.62, 0.9, 0.3, 1.4, 0.3, 2.1, 0.08, 2.6, 0.24, 2.85, 0.0, GajurProfileTop };

        /// <summary>The gajur from its base at local height v0 (on the roof's flat top), <paramref name="height"/> tall
        /// with its base (the profiles are drawn for 5.2 m and scaled).</summary>
        public static void Gajur(MeshData m, in Affine3 k, double v0, double height, int lod)
        {
            Affine3 kk = Affine3.Scaling(height / (GajurBaseTop + GajurProfileTop)).Then(SacredDraw.At(k, 0, v0, 0));
            GajurUnit(m, kk, lod);
        }

        /// <summary>The 5.2 m gajur standing on local (0, 0, 0).</summary>
        private static void GajurUnit(MeshData m, in Affine3 k, int lod)
        {
            const double v0 = 0;
            ShapeBrush gold = PashupatiKit.GiltBright, shade = PashupatiKit.GiltRoof;
            // Stepped square base: three tiers, the lower two with small upturned corner horns.
            double[] halves = { 1.12, 0.96, 0.84 }, tops = { 0.2, 0.37, GajurBaseTop };
            double bottom = v0;
            for (int t = 0; t < 3; t++)
            {
                double h = halves[t], top = v0 + tops[t];
                if (lod >= 2) PashupatiKit.Block(m, k, -h, h, bottom, top, -h, h, shade);
                else
                    PashupatiKit.Band(m, k, 0, 0, h, h, new[] { 0.0, bottom, 0.04, bottom + 0.03, 0.04, top - 0.04, 0.08, top }, t == 1 ? gold : shade, true, shade);
                if (t < 2 && lod == 0)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        double cu = (c % 2 == 0 ? 1 : -1) * (h + 0.06), cw = (c < 2 ? 1 : -1) * (h + 0.06), dl = Math.Sqrt(2);
                        Path3 p = PashupatiKit.PathS;
                        p.Add(cu - 0.1 * cu / (h * dl), top - 0.02, cw - 0.1 * cw / (h * dl)).Add(cu + 0.06 * cu / (h * dl), top + 0.03, cw + 0.06 * cw / (h * dl))
                         .Add(cu + 0.12 * cu / (h * dl), top + 0.16, cw + 0.12 * cw / (h * dl));
                        Shapes.Tube(m, k, gold, p, 0.04, 4, true, false, default, 0.015);
                    }
                }
                bottom = top;
            }
            double b0 = v0 + GajurBaseTop;
            Affine3 at = SacredDraw.At(k, 0, b0, 0);
            if (lod >= 2)
            {
                PashupatiKit.LatheOf(m, at, gold, GajurFar2, 8, 0, 0b10);
                return;
            }
            Profile2 pr = PashupatiKit.Prof;
            for (int i = 0; i < GajurProfile.Length / 2; i++) pr.Add(GajurProfile[2 * i], GajurProfile[2 * i + 1], GajurCreaseMask[i]);
            Shapes.Lathe(m, at, gold, pr, PashupatiKit.Seg(20, lod));
            // The ring of lotus petals round the disc on the bell's shoulder.
            int petals = lod == 0 ? 12 : 8;
            for (int i = 0; i < petals; i++)
            {
                double ang = 2 * Math.PI * (i + 0.5) / petals;
                Affine3 pf = Affine3.RotationX(-0.5).Then(Affine3.RotationY(-ang)).Then(SacredDraw.At(at, 0.33 * Math.Sin(ang), 1.6, 0.33 * Math.Cos(ang)));
                Shapes.Ellipsoid(m, pf, gold, 0.09, 0.1, 0.05, lod == 0 ? 5 : 4, false);
            }
            // Four corner pinnacles on the lowest tier, and the gilt rods from their tips up to the parasol's foot that give
            // the finial its A-frame outline (pagoda/ref_01).
            for (int c = 0; c < 4; c++)
            {
                double cu = (c % 2 == 0 ? 1 : -1) * 0.9, cw = (c < 2 ? 1 : -1) * 0.9;
                PashupatiKit.LatheOf(m, Affine3.Scaling(0.92).Then(SacredDraw.At(k, cu, v0 + tops[0], cw)), gold, PinnacleProfile, 8, lod, 0b0001_0110_0001_0010_0110);
                Shapes.Bar(m, k, gold, cu, v0 + tops[0] + 0.88, cw, 0.17 * Math.Sign(cu), b0 + 2.86, 0.17 * Math.Sign(cw), 0.025, lod == 0 ? 5 : 3);
            }
            // The streamer: a separate dark band hanging in a slight catenary from the parasol's top to the north-east
            // corner of the base platform (c_area/c_26), with round gilt plates along it.
            Path3 sp = PashupatiKit.PathS;
            Curves.Catenary(sp, 0.07, b0 + 4.32, 0.05, 0.95, v0 + tops[2] + 0.03, 0.95, 0.18, lod == 0 ? 10 : 5);
            Profile2 ribbon = PashupatiKit.Prof2;
            ribbon.SetRect(0.12, 0.014);
            Shapes.Sweep(m, k, StreamerDark, sp, ribbon, false, true, SweepFrames.ParallelTransport);
            if (lod == 0)
            {
                for (int q = 1; q < sp.Count - 1; q++)
                {
                    double tx = sp.X[q + 1] - sp.X[q - 1], ty = sp.Y[q + 1] - sp.Y[q - 1], tz = sp.Z[q + 1] - sp.Z[q - 1];
                    Affine3 plate = Affine3.Along(sp.X[q] - 0.012 * tx, sp.Y[q] - 0.012 * ty, sp.Z[q] - 0.012 * tz, sp.X[q] + 0.012 * tx, sp.Y[q] + 0.012 * ty,
                                                  sp.Z[q] + 0.012 * tz, out double _).Then(k);
                    Shapes.Cylinder(m, plate, gold, 0.07, 0.03, 8, 0, 0, true, true);
                }
            }
            // The trident on its pole beside the finial (north side), its round damaru disc below the prongs, braced to
            // the bell.
            double pu = 0.25, pw = 1.0, p0 = v0 + tops[0], p1 = v0 + 3.6;
            Shapes.Bar(m, k, shade, pu, p0, pw, pu, p1, pw, 0.035, lod == 0 ? 6 : 4);
            Path3 c1 = PashupatiKit.PathS;
            c1.Add(pu, p1 - 0.05, pw).Add(pu, p1 + 0.6, pw);
            Shapes.Tube(m, k, gold, c1, 0.045, 4, true, false, default, 0.005);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Path3 c2 = PashupatiKit.PathS;
                c2.Add(pu, p1, pw).Add(pu + sg * 0.17, p1 + 0.08, pw).Add(pu + sg * 0.24, p1 + 0.24, pw).Add(pu + sg * 0.18, p1 + 0.48, pw);
                Shapes.Tube(m, k, gold, c2, 0.035, 4, true, false, default, 0.004);
            }
            // The damaru: a 0.3 m drum disc on the pole, its face to the side (c_area/c_26, c_28).
            Shapes.Cylinder(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(k, pu + 0.04, p1 - 0.45, pw)), gold, 0.15, 0.08, lod == 0 ? 14 : 8, 0.01, 1,
                            true, true);
            Shapes.Bar(m, k, shade, pu, v0 + 1.7, pw, 0.08, b0 + 1.5, 0.36, 0.015, 4);
        }

        private static readonly ShapeBrush StreamerDark = PashupatiKit.Br(0x7A5E26, MaterialChannel.Gilt);

        // ---------------------------------------------------------------------------------------------------------
        // LOD3
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The far shape (≤ 200 triangles): plinth and walls, the two gilt pyramids each over its black band, and
        /// the gajur as a six-sided lathe (bell, pot, parasol cone).</summary>
        private static void Far(ref PashupatiFrame f, MeshData m)
        {
            Affine3 k = f.Site;
            PashupatiKit.Block(m, k, -9.6, 9.6, -0.3, PlinthM, -9.7, 9.7, PashupatiKit.StonePlinth);
            PashupatiKit.Block(m, k, -WallA, WallA, PlinthM, CorniceTopM, -WallB, WallB, PashupatiKit.StoneWall);
            FarTier(m, k, WallA + 0.3, WallB + 0.3, CorniceTopM, LowerEaveA, LowerEaveB, LowerEaveM, UpperA, UpperB, LowerTopM);
            PashupatiKit.Block(m, k, -UpperA, UpperA, LowerTopM - 0.3, UpperCorniceM, -UpperB, UpperB, PashupatiKit.WoodDark);
            FarTier(m, k, UpperA + 0.25, UpperB + 0.25, UpperCorniceM, UpperEaveA, UpperEaveB, UpperEaveM, RoofTopHalf, RoofTopHalf, RoofTopM);
            double s = (TempleTopMetres - RoofTopM - 0.1) / (GajurBaseTop + GajurProfileTop);
            PashupatiKit.LatheOf(m, Affine3.Scaling(s).Then(SacredDraw.At(k, 0, RoofTopM + 0.1 + GajurBaseTop * s, 0)), PashupatiKit.GiltBright, GajurFar3, 6, 0);
        }

        private const double TempleTopMetres = PashupatiSite.TempleTopM;

        private static void FarTier(MeshData m, in Affine3 k, double wu, double ww, double wv, double eu, double ew, double ev, double tu, double tw, double tv)
        {
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, cu, cw, du, dw, eu0, ew0, eu1, ew1;
                PashupatiKit.OnRect(s, -1, wu, ww, out au, out aw);
                PashupatiKit.OnRect(s, 1, wu, ww, out bu, out bw);
                PashupatiKit.OnRect(s, -1, eu, ew, out eu0, out ew0);
                PashupatiKit.OnRect(s, 1, eu, ew, out eu1, out ew1);
                PashupatiKit.OnRect(s, -1, tu, tw, out cu, out cw);
                PashupatiKit.OnRect(s, 1, tu, tw, out du, out dw);
                double ou, ow;
                PashupatiKit.Out(s, out ou, out ow);
                SacredDraw.Quad(m, k, au, wv, aw, bu, wv, bw, eu1, ev - 0.3, ew1, eu0, ev - 0.3, ew0, ou, -0.6, ow, PashupatiKit.Lattice);
                SacredDraw.Quad(m, k, eu0, ev - 0.3, ew0, eu1, ev - 0.3, ew1, eu1, ev, ew1, eu0, ev, ew0, ou, 0, ow, PashupatiKit.GiltEave);
                SacredDraw.Quad(m, k, eu0, ev, ew0, eu1, ev, ew1, du, tv, dw, cu, tv, cw, ou, 0.8, ow, PashupatiKit.GiltRoof);
            }
        }
    }
}
