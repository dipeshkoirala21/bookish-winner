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

        /// <summary>Door opening (silver leaves) and its sill height.</summary>
        public const double DoorW = 1.8, DoorH = 3.2;

        [ThreadStatic] private static double[] _pa, _pb;

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
            PashupatiKit.Skirt(m, k, skirtWU, skirtWW, CorniceTopM, LowerEaveA - 0.06, LowerEaveB - 0.06, LowerEaveM - fascia, 0.24, lod);
            int struts = PashupatiKit.Struts(m, k, WallA, WallB, CorniceTopM - 0.12, LowerEaveA, LowerEaveB, LowerEaveM - fascia, LowerStruts, LowerStruts, 0.86, 101u,
                                             lod);
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
            PashupatiKit.Skirt(m, k, UpperA + 0.25, UpperB + 0.25, UpperCorniceM, UpperEaveA - 0.05, UpperEaveB - 0.05, UpperEaveM - ufascia, 0.22, lod);
            struts += PashupatiKit.Struts(m, k, UpperA, UpperB, UpperCorniceM - 0.1, UpperEaveA, UpperEaveB, UpperEaveM - ufascia, UpperStruts, UpperStruts, 0.84,
                                          203u, lod);
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

        private static void Face(ref PashupatiFrame f, MeshData m, int s, int lod)
        {
            double du, dw;
            PashupatiKit.Out(s, out du, out dw);
            double off = s % 2 == 0 ? WallB : WallA, half = s % 2 == 0 ? WallA : WallB;
            Affine3 x = f.Facing(du * off, 0, dw * off, du, dw);
            double y0 = PlinthM;
            // The wall field: grey ashlar with a proud base course (one quad strip per face; corners closed by the quoins).
            SacredDraw.Box(m, x, -half, half, y0, WallTopM, -0.4, 0, PashupatiKit.StoneWall, BoxFaces.Front);
            SacredDraw.Box(m, x, -half - 0.06, half + 0.06, y0, y0 + 0.42, -0.4, 0.07, PashupatiKit.StoneDark, BoxFaces.Front | BoxFaces.Top);
            // Corner quoins (they also close the corners of the wall boxes).
            for (int sg = -1; sg <= 1; sg += 2)
                SacredDraw.Box(m, x, sg > 0 ? half - 0.36 : -half, sg > 0 ? half : -half + 0.36, y0, WallTopM, -0.4, 0.05, PashupatiKit.StoneEdge,
                               BoxFaces.Front | (sg > 0 ? BoxFaces.Right : BoxFaces.Left));
            Door(m, x, y0, lod);
            if (lod >= 2)
            {
                // Far: the gilt frames as two flat bands and a dark band for the frieze.
                SacredDraw.Box(m, x, -half + 0.4, half - 0.4, 4.35, 4.85, 0, 0.08, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
                for (int sg = -1; sg <= 1; sg += 2)
                    SacredDraw.Box(m, x, sg > 0 ? 1.3 : -half + 0.5, sg > 0 ? half - 0.5 : -1.3, y0 + 0.6, 3.9, 0, 0.05, PashupatiKit.GiltShade, BoxFaces.Front);
                return;
            }
            for (int sg = -1; sg <= 1; sg += 2)
            {
                // The niche with its gold guardian beside the door.
                Niche(m, x, sg * 1.85, y0 + 0.55, 1.0, 2.15, lod, sg);
                // Two gilt-framed repoussé panels, and the upper row of four small panels.
                Panel(m, x, sg * 3.3, y0 + 0.6, 1.25, 2.4, lod);
                Panel(m, x, sg * (half - 1.25), y0 + 0.6, 1.4, 2.4, lod);
                for (int q = 0; q < 4; q++) Panel(m, x, sg * (1.85 + q * (half - 2.3) / 3.4), 5.05, 0.78, 1.15, lod);
            }
            // The carved frieze of small heads at 4.35-4.85 m (behind the torana in the middle).
            SacredDraw.Box(m, x, -half + 0.36, half - 0.36, 4.35, 4.85, 0, 0.1, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            SacredDraw.Box(m, x, -half + 0.36, half - 0.36, 4.3, 4.36, 0, 0.13, PashupatiKit.GiltEave, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            SacredDraw.Box(m, x, -half + 0.36, half - 0.36, 4.84, 4.9, 0, 0.13, PashupatiKit.GiltEave, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            if (lod == 0)
            {
                int heads = (int)Math.Floor((2 * half - 0.9) / 0.42);
                for (int q = 0; q < heads; q++)
                {
                    double u = -half + 0.45 + 0.42 * (q + 0.5);
                    if (Math.Abs(u) < 1.45) continue;
                    Shapes.Sphere(m, SacredDraw.At(x, u, 4.6, 0.15), PashupatiKit.Br(0x5A4636, MaterialChannel.WoodCarved), 0.1, 5);
                }
            }
        }

        /// <summary>The closed door: silver repoussé double leaves with gilt borders, the gilt frame with its ears, the sill
        /// and the semicircular gilt torana with its deity and kirtimukha.</summary>
        private static void Door(MeshData m, in Affine3 x, double y0, int lod)
        {
            double hw = 0.5 * DoorW, top = y0 + DoorH;
            // Leaves, recessed 6 cm, with a split down the middle.
            SacredDraw.Panel(m, x, -hw, y0, hw, top, 0.01, PashupatiKit.Silver);
            if (lod <= 1)
            {
                SacredDraw.Box(m, x, -0.025, 0.025, y0, top, 0.01, 0.04, PashupatiKit.GiltShade, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                // Raised panels on each leaf (three rows) and round bosses.
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    for (int r = 0; r < 3; r++)
                    {
                        double c = sg * 0.45, v0 = y0 + 0.25 + r * 0.98, v1 = v0 + 0.8;
                        SacredDraw.Box(m, x, c - 0.32, c + 0.32, v0, v1, 0.01, 0.045, PashupatiKit.GiltShade, BoxFaces.Front | BoxFaces.Wall);
                        SacredDraw.Box(m, x, c - 0.26, c + 0.26, v0 + 0.06, v1 - 0.06, 0.045, 0.06, PashupatiKit.SilverDark, BoxFaces.Front);
                        if (lod == 0) Shapes.Sphere(m, SacredDraw.At(x, c, 0.5 * (v0 + v1), 0.07), PashupatiKit.Silver, 0.08, 6);
                    }
                }
            }
            // Gilt frame: jambs and a lintel with ears, and the sill.
            const double J = 0.2;
            BoxFaces wall = lod >= 2 ? BoxFaces.Front : BoxFaces.Wall | BoxFaces.Top;
            SacredDraw.Box(m, x, -hw - J, -hw, y0, top, 0, 0.16, PashupatiKit.GiltRoof, wall);
            SacredDraw.Box(m, x, hw, hw + J, y0, top, 0, 0.16, PashupatiKit.GiltRoof, wall);
            SacredDraw.Box(m, x, -hw - J - 0.25, hw + J + 0.25, top, top + J, 0, 0.18, PashupatiKit.GiltRoof, wall | BoxFaces.Bottom);
            SacredDraw.Box(m, x, -hw - J - 0.15, hw + J + 0.15, y0 - 0.02, y0 + 0.12, 0, 0.2, PashupatiKit.StoneEdge, wall);
            if (lod <= 1)
            {
                SacredDraw.Box(m, x, -hw - J - 0.12, -hw - J, y0, top, 0, 0.1, PashupatiKit.SilverDark, BoxFaces.Front | BoxFaces.Left);
                SacredDraw.Box(m, x, hw + J, hw + J + 0.12, y0, top, 0, 0.1, PashupatiKit.SilverDark, BoxFaces.Front | BoxFaces.Right);
            }
            // The torana, 2.6 m, over the lintel.
            SacredParts.Torana(m, x, 0, top + J + 0.02, 0.02, 2.6, PashupatiKit.GiltBright, lod);
        }

        /// <summary>A gilt-framed niche at (u, v) of a face (width w, height h): a dark recess and a gold guardian image
        /// (a standing figure with a crown and an aureole) on a small lotus base; the frame arched at the top.</summary>
        private static void Niche(MeshData m, in Affine3 x, double u, double v, double w, double h, int lod, int side)
        {
            double hw = 0.5 * w;
            SacredDraw.Panel(m, x, u - hw, v, u + hw, v + h, 0.012, PashupatiKit.Ink);
            BoxFaces f = BoxFaces.Front | BoxFaces.Wall;
            SacredDraw.Box(m, x, u - hw - 0.1, u - hw, v, v + h, 0, 0.1, PashupatiKit.GiltRoof, f);
            SacredDraw.Box(m, x, u + hw, u + hw + 0.1, v, v + h, 0, 0.1, PashupatiKit.GiltRoof, f);
            SacredDraw.Box(m, x, u - hw - 0.14, u + hw + 0.14, v - 0.1, v, 0, 0.14, PashupatiKit.GiltRoof, f);
            // The cusped arch over the niche (a small torana).
            SacredParts.Torana(m, x, u, v + h - 0.02, 0.02, w + 0.2, PashupatiKit.GiltRoof, Math.Max(1, lod));
            // The guardian image (gold, about 1.5 m): lotus base, legs, torso, arms, head, crown, aureole.
            Affine3 g = SacredDraw.At(x, u, v, 0.18);
            ShapeBrush gold = PashupatiKit.GiltBright;
            if (lod >= 1)
            {
                Shapes.Capsule(m, SacredDraw.At(g, 0, 0.1, 0), gold, 0.2, 1.4, 6);
                return;
            }
            Shapes.Frustum(m, g, PashupatiKit.GiltShade, 0.32, 0.26, 0.12, 8, 0, 0, false, true);
            Shapes.Frustum(m, SacredDraw.At(g, 0, 0.12, 0), gold, 0.2, 0.16, 0.6, 6, 0, 0, false, false);
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.95, 0), gold, 0.19, 0.27, 0.11, 6, false);
            Shapes.Sphere(m, SacredDraw.At(g, 0, 1.33, 0.02), gold, 0.11, 6);
            Shapes.Cone(m, SacredDraw.At(g, 0, 1.4, 0.02), gold, 0.09, 0.2, 6, 0, 0, false);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(g, 0, 1.35, -0.08)), PashupatiKit.GiltShade, 0.24, 0.03, 10, 0, 0, false, true);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, g, gold, sg * 0.16, 1.1, 0.02, sg * 0.3, 0.8, 0.06, 0.045, 4);
                Shapes.Bar(m, g, gold, sg * 0.15, 1.05, 0.04, sg * 0.32, 1.25, 0.06, 0.04, 4);
            }
            // A mace (gada) held on the outer side.
            Shapes.Bar(m, g, PashupatiKit.GiltShade, side * 0.34, 0.15, 0.08, side * 0.34, 1.0, 0.08, 0.03, 4);
            Shapes.Sphere(m, SacredDraw.At(g, side * 0.34, 1.02, 0.08), gold, 0.07, 5);
        }

        private static readonly ShapeBrush PanelField = PashupatiKit.Br(0x3C3936, MaterialChannel.Metal);
        private static readonly ShapeBrush Repousse = PashupatiKit.Br(0x8E8A82, MaterialChannel.Metal);

        /// <summary>A gilt-framed panel of dark repoussé centred at u from v (width w, height h): the blackened field and,
        /// in silver relief, a standing deity under a gilt aureole (c_inner/c_01, nandi/ref_03: the dark wall panels with
        /// their figures between gilt bands); the small upper panels carry a seated figure.</summary>
        private static void Panel(MeshData m, in Affine3 x, double u, double v, double w, double h, int lod)
        {
            double hw = 0.5 * w, fw = 0.08;
            SacredDraw.Box(m, x, u - hw, u + hw, v, v + h, 0, 0.03, PanelField, BoxFaces.Front);
            BoxFaces f = lod >= 1 ? BoxFaces.Front : BoxFaces.Front | BoxFaces.Wall;
            SacredDraw.Box(m, x, u - hw - fw, u - hw, v - fw, v + h + fw, 0, 0.07, PashupatiKit.GiltRoof, f);
            SacredDraw.Box(m, x, u + hw, u + hw + fw, v - fw, v + h + fw, 0, 0.07, PashupatiKit.GiltRoof, f);
            SacredDraw.Box(m, x, u - hw, u + hw, v - fw, v, 0, 0.07, PashupatiKit.GiltRoof, f);
            SacredDraw.Box(m, x, u - hw, u + hw, v + h, v + h + fw, 0, 0.07, PashupatiKit.GiltRoof, f);
            if (lod != 0) return;
            // The relief: aureole, robe, torso, head and crown, the arms out to the attributes.
            bool tall = h > 1.5 * w;
            double fh = 0.82 * h, base0 = v + 0.06 * h;
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, base0 + 0.78 * fh, 0.035), PashupatiKit.GiltShade, 0.3 * w, (tall ? 0.16 : 0.24) * h, 0.012, 6, false);
            if (tall)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(x, u, base0 + 0.32 * fh, 0.04), Repousse, 0.2 * w, 0.32 * fh, 0.04, 6, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, u, base0 + 0.66 * fh, 0.045), Repousse, 0.16 * w, 0.13 * fh, 0.04, 6, false);
                for (int sg = -1; sg <= 1; sg += 2)
                    Shapes.Bar(m, x, Repousse, u + sg * 0.12 * w, base0 + 0.7 * fh, 0.06, u + sg * 0.32 * w, base0 + 0.52 * fh, 0.06, 0.035, 3);
            }
            else
            {
                // Seated (the small upper panels).
                Shapes.Ellipsoid(m, SacredDraw.At(x, u, base0 + 0.2 * fh, 0.04), Repousse, 0.3 * w, 0.12 * fh, 0.04, 5, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, u, base0 + 0.48 * fh, 0.045), Repousse, 0.17 * w, 0.18 * fh, 0.04, 5, false);
            }
            Shapes.Sphere(m, SacredDraw.At(x, u, base0 + (tall ? 0.82 : 0.72) * fh, 0.05), Repousse, (tall ? 0.09 : 0.1) * w, 5);
            Shapes.Cone(m, SacredDraw.At(x, u, base0 + (tall ? 0.87 : 0.8) * fh, 0.05), PashupatiKit.GiltBright, 0.07 * w, 0.12 * fh, 5, 0, 0, false);
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

        /// <summary>Profile of the gajur above its stepped base as (radius, height) pairs (c_area/c_27, c_28): the great bell
        /// with its nearly straight flaring wall and round shoulder, the lotus disc, the round pot (kalasha), a disc, a small
        /// bell and a knob, the long slender stem, a small knob and the tiered conical parasol with its fringes to the tip.</summary>
        private static readonly double[] GajurProfile =
        {
            0, 0, 0.66, 0, 0.66, 0.06, 0.6, 0.14, 0.55, 0.35, 0.51, 0.7, 0.49, 0.95, 0.45, 1.08, 0.36, 1.16, 0.26, 1.2, 0.16, 1.24, 0.4, 1.26,
            0.44, 1.32, 0.38, 1.4, 0.22, 1.43, 0.33, 1.5, 0.4, 1.6, 0.39, 1.72, 0.3, 1.82, 0.14, 1.88, 0.24, 1.9, 0.24, 1.95, 0.11, 1.97, 0.19, 1.99,
            0.18, 2.06, 0.14, 2.17, 0.07, 2.24, 0.11, 2.27, 0.11, 2.34, 0.05, 2.4, 0.045, 2.42, 0.04, 3.28, 0.08, 3.3, 0.08, 3.36, 0.04, 3.38,
            0.31, 3.41, 0.31, 3.46, 0.24, 3.51, 0.25, 3.55, 0.19, 3.62, 0.2, 3.66, 0.14, 3.73, 0.14, 3.77, 0.09, 3.84, 0.09, 3.88, 0.05, 3.96,
            0.015, 4.08, 0, 4.14,
        };

        /// <summary>Creases of <see cref="GajurProfile"/> (hard rings at discs, rims and the parasol's tiers).</summary>
        private static readonly int[] GajurCreases = { 1, 10, 11, 13, 14, 19, 20, 21, 22, 23, 26, 29, 30, 31, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 };

        /// <summary>A corner pinnacle (lotus collar, bell, two knobs, spike) as (radius, height) pairs.</summary>
        private static readonly double[] PinnacleProfile =
        {
            // A bell on a lotus collar (c_area/c_27, c_area/c_28: the corner bells are inverted cups with a flared rim, about
            // a third of the great bell's height, with a disc, two knobs and a short spike on top).
            0, 0, 0.24, 0, 0.3, 0.04, 0.3, 0.08, 0.22, 0.11, 0.33, 0.13, 0.31, 0.17, 0.28, 0.22, 0.27, 0.34, 0.24, 0.44, 0.17, 0.51, 0.08, 0.54,
            0.11, 0.56, 0.11, 0.6, 0.06, 0.62, 0.09, 0.67, 0.08, 0.73, 0.04, 0.76, 0.06, 0.79, 0.05, 0.84, 0.02, 0.87, 0.012, 0.98, 0, 1.0,
        };

        /// <summary>The gajur from its base at local height v0 (on the roof's flat top), <paramref name="height"/> tall
        /// with its base (the profiles are drawn for 4.6 m and scaled).</summary>
        public static void Gajur(MeshData m, in Affine3 k, double v0, double height, int lod)
        {
            Affine3 kk = Affine3.Scaling(height / 4.6).Then(SacredDraw.At(k, 0, v0, 0));
            GajurUnit(m, kk, lod);
        }

        /// <summary>The 4.6 m gajur standing on local (0, 0, 0).</summary>
        private static void GajurUnit(MeshData m, in Affine3 k, int lod)
        {
            const double v0 = 0;
            ShapeBrush gold = PashupatiKit.GiltBright, shade = PashupatiKit.GiltRoof;
            // Stepped square base: three tiers, the lower two with small upturned corner horns.
            double[] halves = { 1.12, 0.96, 0.84 }, tops = { 0.18, 0.34, 0.46 };
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
            double b0 = v0 + tops[2];
            Affine3 at = SacredDraw.At(k, 0, b0, 0);
            if (lod >= 2)
            {
                PashupatiKit.LatheOf(m, at, gold, new[] { 0.0, 0.0, 0.84, 0.0, 0.62, 0.85, 0.2, 1.3, 0.42, 1.7, 0.14, 2.0, 0.2, 2.3, 0.04, 3.0, 0.3, 3.3, 0.0, 4.14 }, 8, lod,
                                     0b10);
                return;
            }
            Profile2 pr = PashupatiKit.Prof;
            int ci = 0;
            for (int i = 0; i < GajurProfile.Length / 2; i++)
            {
                bool crease = ci < GajurCreases.Length && GajurCreases[ci] == i;
                if (crease) ci++;
                pr.Add(GajurProfile[2 * i], GajurProfile[2 * i + 1], crease);
            }
            Shapes.Lathe(m, at, gold, pr, PashupatiKit.Seg(24, lod));
            // The ring of lotus petals round the top of the bell.
            int petals = lod == 0 ? 14 : 8;
            for (int i = 0; i < petals; i++)
            {
                double ang = 2 * Math.PI * (i + 0.5) / petals;
                Affine3 pf = Affine3.RotationY(-ang).Then(SacredDraw.At(at, 0.42 * Math.Sin(ang), 1.33, 0.42 * Math.Cos(ang)));
                Shapes.Ellipsoid(m, pf, gold, 0.12, 0.11, 0.08, lod == 0 ? 6 : 4, false);
            }
            // Four corner pinnacles on the lowest tier.
            for (int c = 0; c < 4; c++)
            {
                double cu = (c % 2 == 0 ? 1 : -1) * 0.9, cw = (c < 2 ? 1 : -1) * 0.9;
                PashupatiKit.LatheOf(m, Affine3.Scaling(0.92).Then(SacredDraw.At(k, cu, v0 + tops[0], cw)), gold, PinnacleProfile, 12, lod, 0b0001_0110_0001_0010_0110);
            }
            // The streamer: a scalloped gilt ribbon from the parasol down the east face of the stack, over the bell, to the
            // base; round plates along it.
            // It hangs taut from under the parasol, clear of the stem and the pot, to the bell's shoulder, then lies on the
            // bell's wall down to the base.
            // From under the parasol's rim straight down past the pot to the bell's skirt (it hangs free of the stack).
            double[] sr = { 0.3, 0.34, 0.38, 0.43, 0.47, 0.51, 0.55, 0.6, 0.66 }, sh = { 3.38, 3.0, 2.58, 2.12, 1.66, 1.2, 0.74, 0.3, -0.02 };
            int sn = sr.Length, first = m.VertexCount;
            for (int i = 0; i < sn; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(sn - 1, i + 1);
                double tr = sr[b] - sr[a], th = sh[b] - sh[a], tl = Math.Sqrt(tr * tr + th * th);
                double nr = -th / tl, nh = tr / tl;
                if (nr < 0)
                {
                    nr = -nr;
                    nh = -nh;
                }
                for (int sg = -1; sg <= 1; sg += 2)
                    SacredDraw.V(m, at, sr[i], sh[i], sg * 0.065, nr, nh, 0, i % 2 == 0 ? gold : shade);
            }
            SacredDraw.Grid(m, first, sn, 2, false, null);
            if (lod == 0)
            {
                for (int i = 0; i + 1 < sn; i++)
                {
                    for (int q = 0; q < 2; q++)
                    {
                        double t = (q + 0.5) / 2, r0 = sr[i] + (sr[i + 1] - sr[i]) * t, h0 = sh[i] + (sh[i + 1] - sh[i]) * t;
                        Shapes.Sphere(m, SacredDraw.At(at, r0 + 0.03, h0, 0), gold, 0.045, 5);
                    }
                }
            }
            // The trident on its pole beside the finial (north side), its damaru below the prongs, braced to the pot.
            double pu = 0.25, pw = 1.0, p0 = v0 + tops[0], p1 = v0 + 3.15;
            Shapes.Bar(m, k, shade, pu, p0, pw, pu, p1, pw, 0.035, lod == 0 ? 6 : 4);
            Path3 c1 = PashupatiKit.PathS;
            c1.Add(pu, p1 - 0.05, pw).Add(pu, p1 + 0.55, pw);
            Shapes.Tube(m, k, gold, c1, 0.045, 4, true, false, default, 0.005);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Path3 c2 = PashupatiKit.PathS;
                c2.Add(pu, p1, pw).Add(pu + sg * 0.16, p1 + 0.08, pw).Add(pu + sg * 0.22, p1 + 0.22, pw).Add(pu + sg * 0.17, p1 + 0.45, pw);
                Shapes.Tube(m, k, gold, c2, 0.035, 4, true, false, default, 0.004);
            }
            Shapes.Frustum(m, SacredDraw.At(k, pu, p1 - 0.42, pw), shade, 0.09, 0.03, 0.09, 6, 0, 0, true, true);
            Shapes.Frustum(m, SacredDraw.At(k, pu, p1 - 0.33, pw), shade, 0.03, 0.09, 0.09, 6, 0, 0, true, true);
            Shapes.Bar(m, k, shade, pu, v0 + 1.55, pw, 0.08, b0 + 1.6, 0.36, 0.015, 4);
        }

        // ---------------------------------------------------------------------------------------------------------
        // LOD3
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The far shape (≤ 200 triangles): plinth and walls, the two gilt pyramids each over its black band, and
        /// the gajur cone.</summary>
        private static void Far(ref PashupatiFrame f, MeshData m)
        {
            Affine3 k = f.Site;
            PashupatiKit.Block(m, k, -9.6, 9.6, -0.3, PlinthM, -9.7, 9.7, PashupatiKit.StonePlinth);
            PashupatiKit.Block(m, k, -WallA, WallA, PlinthM, CorniceTopM, -WallB, WallB, PashupatiKit.StoneWall);
            FarTier(m, k, WallA + 0.3, WallB + 0.3, CorniceTopM, LowerEaveA, LowerEaveB, LowerEaveM, UpperA, UpperB, LowerTopM);
            PashupatiKit.Block(m, k, -UpperA, UpperA, LowerTopM - 0.3, UpperCorniceM, -UpperB, UpperB, PashupatiKit.WoodDark);
            FarTier(m, k, UpperA + 0.25, UpperB + 0.25, UpperCorniceM, UpperEaveA, UpperEaveB, UpperEaveM, RoofTopHalf, RoofTopHalf, RoofTopM);
            Shapes.Cone(m, SacredDraw.At(k, 0, RoofTopM, 0), PashupatiKit.GiltBright, 0.9, TempleTopMetres - RoofTopM, 6, 0, 0, false);
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
