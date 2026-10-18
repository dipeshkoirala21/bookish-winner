using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// The two gates of the Pashupatinath courtyard (pashupati-temple.md PT-04, PT-11):
    /// <list type="bullet">
    /// <item>the West Gate (main gate) on the door axis, 10 m wide and 6.8 m deep (OSM w913170307), a cream two-storey
    /// pavilion facing the forecourt (306.7°) and, the same, the courtyard: on the ground storey four dark stone columns
    /// with acanthus capitals, the passage with its dark wooden doors under a gilt repoussé torana, painted blue-framed
    /// medallions (Ganesh, a solar deity), moon and sun panels, blind black-arched niches and two seated painted lions at
    /// the column feet; the black lintel band with the green name board; the upper loggia with cream columns, wide arches
    /// and a scrollwork balustrade either side of the big painted relief (blue Shiva with trident before snowy Kailash on
    /// the forecourt face, Shiva and Parvati on the courtyard face) between white vine panels; the pointed gable with the
    /// orange Om on blue; the cream metal hip roof with its red edge trim; the lattice lantern with its red-edged mini roof
    /// and three gilt finials (c_westgate/c_05, c_27; c_gje/c_00);</item>
    /// <item>the East Gate at the top of the river stair: a narrow pale-yellow plastered gate with white pilasters, the
    /// dark door, the blue lunette (Shiva and Parvati) in its green rim, the curved pediment with the Om roundel, a large
    /// gilt bell finial and two small ones, two dark stone lions on the top step; the stone stair (about 25 risers) down to
    /// the ghat walk with a white wall on its south side hung with marigold garlands and the brass bell on its bracket, and
    /// the brass handrail on its north side (c_gje/c_01, c_03, c_04).</item>
    /// </list>
    /// </summary>
    internal static class PashupatiGates
    {
        /// <summary>The West Gate: centre (site a and b), half depth and half width. Its depth and width are the OSM gatehouse
        /// w913170307's (a −40.1..−33.4, 10 m wide), its passage on the temple's door axis (b 0): from the forecourt, square
        /// on, the Nandi's gilt rump sits centred in the opening with the silver west door just beyond it
        /// (c_westgate/c_09, c_05; c_westgate/c_26 from the threshold), and the great trident stands before the gate's north
        /// half (c_gje/c_00). OSM maps the gatehouse 3.45 m south of that and is not consistent with itself: its entrance node
        /// n442150828 (b +3.6) and the footways to it, 185567049 (b +3.6) and 1328801417 (b −3.4), straddle the axis (open
        /// issue for roads and data: end both footways at the gate's front on the axis).</summary>
        public const double WestA = -36.75, WestB = 0.0, WestHalfDepth = 3.4, WestHalfWidth = 4.8;

        /// <summary>Finial tip above the courtyard (12.5 m above the forecourt [P], which stands 0.6 m higher).</summary>
        public const float WestGateTopM = 13.5f;

        /// <summary>The East Gate: front face (site a), centre (site b) and half width; the west edge of the riverside walk
        /// (footway 80913703) the stair comes down to, and the stair's half width (OSM steps w112776516). The gate stands
        /// on its OSM outline with its door on the mapped steps way (centre b 6.65, the drawn way 2.5 m wide).</summary>
        public const double EastFrontA = 24.2, EastB = 6.65, EastHalfWidth = 2.3, WalkEdgeA = 34.75, StairHalfWidth = 1.75;

        /// <summary>The East Gate's door half width (the opening as wide as the drawn steps way at the gate), and the stair's
        /// turn from the gate axis toward compound north (the steps way runs 11° off it to the riverside walk).</summary>
        public const double EastDoorHalf = 1.15, StairSkewRad = 0.192;

        private static readonly ShapeBrush Cream = PashupatiKit.Cream;
        private static readonly ShapeBrush CreamShade = PashupatiKit.Br(0xD9CFAE, MaterialChannel.Plaster);
        private static readonly ShapeBrush ColumnStone = PashupatiKit.Br(0x4A4A48, MaterialChannel.Stone);
        private static readonly ShapeBrush Black = PashupatiKit.Br(0x24201E, MaterialChannel.Paint);
        private static readonly ShapeBrush BoardGreen = PashupatiKit.Br(0x2E7D4A, MaterialChannel.Paint);
        private static readonly ShapeBrush SignYellow = PashupatiKit.Br(0xE8C62E, MaterialChannel.Paint);
        private static readonly ShapeBrush SkyBlue = PashupatiKit.Br(0x72A6CA, MaterialChannel.Paint);
        private static readonly ShapeBrush DeepBlue = PashupatiKit.Br(0x3E7FB8, MaterialChannel.Paint);
        private static readonly ShapeBrush ShivaBlue = PashupatiKit.Br(0x4F86C6, MaterialChannel.Paint);
        private static readonly ShapeBrush Snow = PashupatiKit.Br(0xF2F4F6, MaterialChannel.Paint);
        private static readonly ShapeBrush Water = PashupatiKit.Br(0x5A9E86, MaterialChannel.Paint);
        private static readonly ShapeBrush VineGreen = PashupatiKit.Br(0x3C8A4C, MaterialChannel.Paint);
        private static readonly ShapeBrush LotusRed = PashupatiKit.Br(0xD0453A, MaterialChannel.Paint);
        private static readonly ShapeBrush OmOrange = PashupatiKit.Br(0xE8862A, MaterialChannel.Paint);
        private static readonly ShapeBrush Saffron = PashupatiKit.Br(0xE7A33C, MaterialChannel.Paint);
        private static readonly ShapeBrush RoofCream = PashupatiKit.Br(0xE9E0C2, MaterialChannel.Metal);
        private static readonly ShapeBrush RoofCreamRib = PashupatiKit.Br(0xF2EBD2, MaterialChannel.Metal);
        private static readonly ShapeBrush Gilt = PashupatiKit.GiltBright;
        private static readonly ShapeBrush DadoRed = PashupatiKit.Br(0x5A2E22, MaterialChannel.Paint);
        private static readonly ShapeBrush PaleYellow = PashupatiKit.Br(0xE8DCA8, MaterialChannel.Plaster);
        private static readonly ShapeBrush Pilaster = PashupatiKit.Br(0xF0EEE6, MaterialChannel.Plaster);
        private static readonly ShapeBrush RimGreen = PashupatiKit.Br(0x2E7D5B, MaterialChannel.Paint);
        private static readonly ShapeBrush StairStone = PashupatiKit.Br(0x5C5A55, MaterialChannel.Stone);
        private static readonly ShapeBrush Scroll = PashupatiKit.Br(0x3A2A20, MaterialChannel.Paint);
        private static readonly ShapeBrush ToranaBronze = PashupatiKit.Br(0x6E5530, MaterialChannel.Metal);

        /// <summary>The West Gate's four dark stone columns on each face (across the gate from the passage centre).</summary>
        private static readonly double[] ColumnsU = { -4.3, -2.15, 2.15, 4.3 };

        // ---------------------------------------------------------------------------------------------------------
        // West Gate
        // ---------------------------------------------------------------------------------------------------------

        public static void West(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod, v0 = m.VertexCount, i0 = m.IndexCount;
            double rise = PashupatiSite.ForecourtRiseM;
            // Gate frame: z toward the forecourt (306.7°), x along +b, y up from the forecourt floor.
            Affine3 g = f.Facing(WestA, rise, WestB, -1, 0);
            const double Hw = WestHalfWidth, Hd = WestHalfDepth, Op = 1.5, Ground = 5.3, Eave = 9.3;
            if (lod >= 3)
            {
                PashupatiKit.Block(m, g, -Hw, Hw, -0.6, Eave, -Hd, Hd, Cream);
                Shapes.Cone(m, SacredDraw.At(g, 0, Eave, 0), RoofCream, 6.0, 1.6, 4, 0, 0, false);
                Shapes.Cone(m, SacredDraw.At(g, 0, Eave + 1.4, 0), Gilt, 0.4, WestGateTopM - rise - Eave - 1.4, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level + rise);
                return;
            }
            // Ground storey: the side bays (solid) and the passage ceiling.
            PashupatiKit.Block(m, g, -Hw, -Op, -0.6, Ground, -Hd, Hd, Cream);
            PashupatiKit.Block(m, g, Op, Hw, -0.6, Ground, -Hd, Hd, Cream);
            PashupatiKit.Block(m, g, -Op, Op, 4.3, Ground, -Hd, Hd, Cream);
            SacredDraw.Box(m, g, -Op, Op, 4.29, 4.3, -Hd, Hd, CreamShade, BoxFaces.Bottom);
            // Passage walls: white with the dark red-brown dado; its stone floor at the forecourt level.
            for (int s = -1; s <= 1; s += 2)
                SacredDraw.Box(m, g, s > 0 ? Op - 0.01 : -Op, s > 0 ? Op : -Op + 0.01, 0, 1.25, -Hd, Hd, DadoRed, s > 0 ? BoxFaces.Left : BoxFaces.Right);
            SacredDraw.Quad(m, g, -Op, 0.01, -Hd - 0.3, Op, 0.01, -Hd - 0.3, Op, 0.01, Hd + 0.4, -Op, 0.01, Hd + 0.4, 0, 1, 0,
                            PashupatiKit.Br(0xA9A396, MaterialChannel.Flagstone));
            for (int face = -1; face <= 1; face += 2) GateFace(m, g, face, lod);
            UpperStorey(m, g, lod);
            GateRoof(m, g, lod);
            // Three steps down into the courtyard from the passage (beyond the courtyard face), to the paving at their
            // foot (draped a little where a coarse terrain cell from the forecourt's step reaches under it).
            double foot = FlightFoot(ref f);
            for (int i = 1; i <= 3; i++)
            {
                double top = -(rise - foot) * i / 3.0, w0 = -Hd - 0.32 * i, w1 = -Hd - 0.32 * (i - 1);
                SacredDraw.Box(m, g, -Op - 0.3, Op + 0.3, -rise - 0.05, top + 0.2, w0, w1, PashupatiKit.Br(0x8E897F, MaterialChannel.Stone),
                               BoxFaces.Top | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            }
            SacredDraw.Bake(m, v0, i0, f.Level + rise);

            // Colliders: the two side bays (never climbed), the passage floor, the steps, the columns. The bays and the
            // forecourt columns keep off the ends of the two drawn OSM footways that run into the bays (BoxClear).
            double ga = WestA;
            // (Gate frame x runs along +b from the passage centre at WestB.)
            const double B = WestB;
            f.BoxClear(ga - Hd, ga + Hd, B + Op, B + Hw, rise - 0.6, rise + Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.BoxClear(ga - Hd, ga + Hd, B - Hw, B - Op, rise - 0.6, rise + Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            // The passage floor is a level walkway at the forecourt's height (a ramp, never a blocking solid: the terrain's
            // 8 m grid eases from the forecourt to the courtyard under it), and the flight down to the courtyard a second.
            f.Ramp(ga - Hd - 0.2, B, rise, ga + Hd, B, rise, Op, GenColliders.Stone);
            f.Ramp(ga + Hd + 0.96, B, foot, ga + Hd, B, rise, Op, GenColliders.Stone);
            foreach (double cx in ColumnsU)
                for (int face = -1; face <= 1; face += 2)
                    f.CylinderClear(ga - face * (Hd + 0.32), B + cx, rise, rise + Ground, 0.32);
        }

        /// <summary>The courtyard paving's height under the foot of the West Gate's flight (above the courtyard): its
        /// level, or the drape over the terrain's coarse grid (<see cref="PashupatiFrame.TopGroundAt"/>), at most a
        /// step below the passage.</summary>
        private static double FlightFoot(ref PashupatiFrame f)
        {
            double foot = 0.02;
            for (int i = 0; i < 3; i++)
                for (int j = -1; j <= 1; j++)
                    foot = Math.Max(foot, f.TopGroundAt(WestA + WestHalfDepth + 0.96 * i, WestB + 1.8 * j, 0) + 0.02);
            return Math.Min(foot, PashupatiSite.ForecourtRiseM - 0.3);
        }

        /// <summary>A face frame of the West Gate: z out of the forecourt face (+1) or the courtyard face (−1).</summary>
        private static Affine3 FaceFrame(in Affine3 g, int face, double depth)
        {
            return face > 0 ? SacredDraw.At(g, 0, 0, depth) : SacredDraw.Basis(g, 0, 0, -depth, -1, 0, 0, 0, 1, 0);
        }

        /// <summary>One face of the gate's ground storey (face +1: the forecourt, −1: the courtyard): the four dark stone
        /// columns with their acanthus capitals, the passage frame and torana, the painted medallions, moon and sun panels,
        /// blind niches, the lions (forecourt face) and the black lintel band with the green board.</summary>
        private static void GateFace(MeshData m, in Affine3 g, int face, int lod)
        {
            Affine3 x = FaceFrame(g, face, WestHalfDepth);
            const double Op = 1.5, Ground = 5.3;
            SacredDraw.Box(m, x, -WestHalfWidth - 0.05, -Op, -0.6, 0.3, -0.2, 0.06, PashupatiKit.StoneDark, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left);
            SacredDraw.Box(m, x, Op, WestHalfWidth + 0.05, -0.6, 0.3, -0.2, 0.06, PashupatiKit.StoneDark, BoxFaces.Front | BoxFaces.Top | BoxFaces.Right);
            // Passage frame (dark wood) with the doors folded back against the passage walls.
            SacredDraw.Box(m, x, -Op - 0.22, -Op, 0, 4.3, -0.1, 0.1, PashupatiKit.WoodDoor, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, x, Op, Op + 0.22, 0, 4.3, -0.1, 0.1, PashupatiKit.WoodDoor, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, x, -Op - 0.22, Op + 0.22, 3.55, 3.75, -0.1, 0.12, PashupatiKit.WoodDoor, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2)
                    SacredDraw.Box(m, x, s * (Op - 0.12) - 0.06, s * (Op - 0.12) + 0.06, 0, 3.55, -1.6, -0.1, PashupatiKit.WoodDoor, BoxFaces.Left | BoxFaces.Right);
            // The gilt torana in the arch over the doors, in its black arch frame.
            int tor = m.VertexCount;
            SacredParts.Torana(m, x, 0, 3.78, 0.02, 3.2, ToranaBronze, lod);
            // The West Gate's torana is dark carved bronze (c_westgate/c_05), its relief included.
            ShapeColor.Tint(m, tor, m.VertexCount - tor, 0x8C7058FFu);
            Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, 0, 3.78, 0.04)), Black, 1.7, 0.09, lod == 0 ? 20 : 10, 4, default, 90, 180, true);
            foreach (double cx in ColumnsU) Column(m, x, cx, 0.32, Ground, lod);
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double cu = s * 3.3;
                    // The painted medallion in a blue cusped-arch frame (Ganesh, a solar deity), the small blue moon or sun
                    // panel above it, the shelf under it and the blind black-arched niche below.
                    SacredDraw.Box(m, x, cu - 0.62, cu + 0.62, 1.9, 3.3, 0, 0.05, DeepBlue, BoxFaces.Front);
                    SacredParts.Torana(m, x, cu, 3.28, 0.02, 1.24, DeepBlue, Math.Max(1, lod));
                    // Ganesh on the viewer's left (u > 0 in a face frame), a seated solar deity on the right (c_westgate/c_05).
                    SeatedDeity(m, x, cu, 1.98, 0.06, 1.22, s > 0, lod);
                    SacredDraw.Box(m, x, cu - 0.45, cu + 0.45, 1.75, 1.92, 0, 0.12, LotusRed, BoxFaces.Wall);
                    SacredDraw.Box(m, x, cu - 0.42, cu + 0.42, 3.9, 4.6, 0, 0.04, DeepBlue, BoxFaces.Front);
                    // The moon's white face over Ganesh, the sun's orange face with its rays over the other.
                    Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, cu, 4.25, 0.06)), s > 0 ? Snow : OmOrange, 0.2, 0.02, 10, 0, 0, false,
                                    true);
                    if (s < 0 && lod == 0)
                        for (int q = 0; q < 8; q++)
                        {
                            double ra = 2 * Math.PI * q / 8;
                            Shapes.Bar(m, x, Saffron, cu + 0.22 * Math.Cos(ra), 4.25 + 0.22 * Math.Sin(ra), 0.07, cu + 0.31 * Math.Cos(ra), 4.25 + 0.31 * Math.Sin(ra), 0.07,
                                       0.025, 3);
                        }
                    if (lod == 0)
                    {
                        // The face on the disc: eyes and mouth.
                        for (int e = -1; e <= 1; e += 2) Shapes.Sphere(m, SacredDraw.At(x, cu + e * 0.07, 4.3, 0.08), PashupatiKit.Ink, 0.025, 4);
                        Shapes.Bar(m, x, PashupatiKit.Ink, cu - 0.06, 4.17, 0.08, cu + 0.06, 4.17, 0.08, 0.012, 3);
                    }
                    SacredDraw.Box(m, x, cu - 0.4, cu + 0.4, 0.3, 1.3, 0, 0.02, Black, BoxFaces.Front);
                    SacredParts.Torana(m, x, cu, 1.28, 0.01, 0.8, Black, 2);
                    SacredDraw.Box(m, x, cu - 0.6, cu + 0.6, 1.55, 1.7, 0, 0.25, PashupatiKit.WoodDark, BoxFaces.Wall | BoxFaces.Bottom);
                }
                if (face > 0)
                {
                    // Two seated guardian lions, painted orange-gold, on low plinths at the outer column feet.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        SacredParts.Pedestal(m, x, s * 3.85, 0, 0.75, 0.32, 0.4, 0.45, PashupatiKit.StoneDark, lod);
                        int first = m.VertexCount;
                        SacredFigures.Guardian(m, x, s * 3.85, 0.45, 0.75, 1.15, GuardianKind.Lion, true, SacredFigures.Small, lod, s);
                        ShapeColor.Tint(m, first, m.VertexCount - first, 0xD8823AFFu);
                    }
                }
            }
            // The black lintel band on the column capitals, with the green name board (ornament, no readable text).
            SacredDraw.Box(m, x, -WestHalfWidth - 0.1, WestHalfWidth + 0.1, Ground, Ground + 1.0, 0, 0.65, Black, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            // The name board is on the forecourt face only (c_gje/c_00: the courtyard face has none).
            if (face < 0) return;
            SacredDraw.Box(m, x, -1.4, 1.4, Ground + 0.25, Ground + 0.75, 0.65, 0.68, BoardGreen, BoxFaces.Front);
            if (lod == 0)
                for (int q = 0; q < 9; q++)
                    SacredDraw.Box(m, x, -1.1 + 0.275 * q - 0.08, -1.1 + 0.275 * q + 0.08, Ground + 0.4, Ground + 0.6, 0.68, 0.69, SignYellow, BoxFaces.Front);
        }

        /// <summary>A dark stone column at u on face frame f (z out): square base, round shaft, acanthus capital.</summary>
        private static void Column(MeshData m, in Affine3 f, double u, double z, double h, int lod)
        {
            Affine3 at = SacredDraw.At(f, u, 0, z);
            if (lod >= 2)
            {
                Shapes.Cylinder(m, at, ColumnStone, 0.28, h, 6, 0, 0, false, false);
                return;
            }
            if (lod == 0) Shapes.RoundedBox(m, SacredDraw.At(at, 0, 0.25, 0), ColumnStone, 0.75, 0.5, 0.75, 0.04, 1);
            else PashupatiKit.Block(m, at, -0.375, 0.375, 0, 0.5, -0.375, 0.375, ColumnStone);
            PashupatiKit.LatheOf(m, at, ColumnStone,
                                 new[]
                                 {
                                     0.0, 0.5, 0.33, 0.5, 0.3, 0.62, 0.275, 0.7, 0.26, h - 0.95, 0.3, h - 0.9, 0.3, h - 0.82, 0.26, h - 0.78, 0.3, h - 0.45, 0.42, h - 0.2,
                                     0.42, h - 0.12, 0.0, h - 0.12,
                                 }, 8, lod, 0b0101_1101_1111);
            if (lod == 0)
                for (int i = 0; i < 4; i++)
                {
                    double a = 2 * Math.PI * (i + 0.5) / 4;
                    Shapes.Ellipsoid(m, SacredDraw.At(at, 0.33 * Math.Sin(a), h - 0.45, 0.33 * Math.Cos(a)), ColumnStone, 0.11, 0.16, 0.11, 4, false);
                }
            if (lod == 0) Shapes.RoundedBox(m, SacredDraw.At(at, 0, h - 0.06, 0), ColumnStone, 0.9, 0.12, 0.9, 0.02, 1);
            else PashupatiKit.Block(m, at, -0.45, 0.45, h - 0.12, h, -0.45, 0.45, ColumnStone);
        }

        /// <summary>The upper storey (c_westgate/c_27, c_gje/c_00): floor slab, the end walls, the solid core carrying the
        /// painted reliefs (both faces) between white vine panels, and either side of it an open loggia through the depth of
        /// the gate: round cream columns at both faces, a wide round arch on each face with its filled spandrels, the
        /// balustrade with its dark scrollwork, and the dark scroll frieze under the cornice.</summary>
        private static void UpperStorey(MeshData m, in Affine3 g, int lod)
        {
            const double Hw = WestHalfWidth, Hd = WestHalfDepth, Floor = 6.3, Eave = 9.3, Core = 1.85, ArchTop = Floor + 2.55, ArchRise = 0.45, Rail = 0.6;
            PashupatiKit.Block(m, g, -Hw - 0.1, Hw + 0.1, Floor - 0.05, Floor + 0.1, -Hd - 0.3, Hd + 0.3, Cream);
            PashupatiKit.Block(m, g, -Hw, -Hw + 0.35, Floor, Eave, -Hd, Hd, Cream);
            PashupatiKit.Block(m, g, Hw - 0.35, Hw, Floor, Eave, -Hd, Hd, Cream);
            PashupatiKit.Block(m, g, -Core, Core, Floor, Eave, -Hd + 0.15, Hd - 0.15, Cream);
            for (int s = -1; s <= 1; s += 2)
            {
                double u0 = s > 0 ? Core : -Hw + 0.35, u1 = s > 0 ? Hw - 0.35 : -Core;
                // The loggia's ceiling (shaded) under the cornice band, its floor's lighter tiles.
                SacredDraw.Box(m, g, u0, u1, ArchTop, Eave, -Hd + 0.3, Hd - 0.3, PashupatiKit.Br(0xB8AE90, MaterialChannel.Plaster, 0.6f), BoxFaces.Bottom);
                SacredDraw.Box(m, g, u0, u1, Floor + 0.1, Floor + 0.11, -Hd + 0.3, Hd - 0.3, PashupatiKit.Br(0xC9C1B2, MaterialChannel.Flagstone, 0.8f), BoxFaces.Top);
                for (int face = -1; face <= 1; face += 2)
                {
                    Affine3 x = FaceFrame(g, face, Hd);
                    // In the face frame of the courtyard face u runs the other way: mirror the bay.
                    double c0 = s * face > 0 ? Core + 0.2 : -Hw + 0.5, c1 = s * face > 0 ? Hw - 0.5 : -Core - 0.2;
                    foreach (double cu in new[] { c0, c1 })
                    {
                        // Tall slender columns from the floor (c_westgate/c_27), the balustrade running between them.
                        Affine3 at = SacredDraw.At(x, cu, Floor + 0.1, -0.25);
                        double ch = ArchTop - ArchRise - Floor - 0.1;
                        if (lod >= 2) Shapes.Cylinder(m, at, Cream, 0.16, ch, 6, 0, 0, false, false);
                        else
                        {
                            PashupatiKit.LatheOf(m, at, Cream, new[] { 0.0, 0.0, 0.22, 0.0, 0.22, 0.12, 0.16, 0.2, 0.15, ch - 0.22, 0.22, ch - 0.1, 0.24, ch, 0.0, ch }, 10,
                                                 lod, 0b1100_0110);
                        }
                    }
                    double lo = Math.Min(c0, c1), hi = Math.Max(c0, c1), span = hi - lo, mid = 0.5 * (lo + hi), R = 0.5 * span - 0.05;
                    double spring = ArchTop - ArchRise;
                    // The wide, shallow arch (c_westgate/c_27): its moulded soffit ring, and the filled spandrels up to the
                    // cornice band.
                    Shapes.Torus(m, Affine3.Scaling(1, 1, ArchRise / R).Then(Affine3.RotationX(0.5 * Math.PI)).Then(SacredDraw.At(x, mid, spring, -0.25)), Cream, R,
                                 0.12, lod == 0 ? 18 : 9, 4, default, 90, 180, true);
                    Spandrel(m, x, lo - 0.2, hi + 0.2, mid, spring, R + 0.1, ArchRise + 0.06, Eave - 0.4, -0.12, lod);
                    // The balustrade: a low cream wall with dark scroll panels (two lions' tails of ironwork in the photos).
                    SacredDraw.Box(m, x, lo - 0.2, hi + 0.2, Floor, Floor + Rail, -0.4, -0.1, Cream, BoxFaces.Wall | BoxFaces.Top);
                    if (lod == 0)
                        for (int q = 0; q < 3; q++)
                        {
                            double su = lo + span * (q + 0.5) / 3;
                            SacredDraw.Box(m, x, su - 0.36, su + 0.36, Floor + 0.12, Floor + Rail - 0.1, -0.1, -0.09, PashupatiKit.Br(0xE3D6B0, MaterialChannel.Plaster),
                                           BoxFaces.Front);
                            Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, su - 0.14, Floor + 0.31, -0.08)), Scroll, 0.11, 0.02, 10, 3, default, 0, 300, true);
                            Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, su + 0.14, Floor + 0.31, -0.08)), Scroll, 0.11, 0.02, 10, 3, default, 180, 300, true);
                        }
                }
            }
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 x = FaceFrame(g, face, Hd - 0.15);
                Relief(m, x, Floor + 0.06, face > 0, lod);
                for (int s = -1; s <= 1; s += 2)
                {
                    // The vine panels either side of the relief: white with a green vine from a blue vase on the forecourt
                    // face (c_westgate/c_23), blue-and-white on the courtyard face (c_gje/c_00).
                    VinePanel(m, x, s * 1.38, Floor + 0.12, Eave - 0.45, face > 0, lod);
                    // The cream pilaster between the white panel and the loggia.
                    SacredDraw.Box(m, x, s * 1.76 - 0.1, s * 1.76 + 0.1, Floor + 0.1, Eave - 0.4, 0, 0.12, CreamShade, BoxFaces.Wall);
                }
            }
            // The cornice and its dark scroll frieze under the eaves.
            PashupatiKit.Band(m, g, 0, 0, Hw, Hd, new[] { 0.0, Eave - 0.4, 0.08, Eave - 0.35, 0.08, Eave - 0.08, 0.2, Eave }, Cream, false, Cream);
            if (lod <= 1)
                SacredDraw.Box(m, g, -Hw - 0.05, Hw + 0.05, Eave - 0.33, Eave - 0.12, -Hd - 0.09, Hd + 0.09, Scroll, BoxFaces.Front | BoxFaces.Back);
        }

        /// <summary>The spandrels of an elliptical arch on face frame x: the wall from the arch's extrados (centre mid,
        /// springing line <paramref name="spring"/>, half span r, rise h) up to <paramref name="top"/>, between u0 and u1,
        /// at depth z, facing +z.</summary>
        private static void Spandrel(MeshData m, in Affine3 x, double u0, double u1, double mid, double spring, double r, double h, double top, double z, int lod)
        {
            int n = lod == 0 ? 12 : 6;
            for (int i = 0; i < n; i++)
            {
                double t0 = Math.PI * i / n, t1 = Math.PI * (i + 1) / n;
                double xa = mid - r * Math.Cos(t0), ya = spring + h * Math.Sin(t0), xb = mid - r * Math.Cos(t1), yb = spring + h * Math.Sin(t1);
                SacredDraw.Quad(m, x, xa, ya, z, xb, yb, z, xb, top, z, xa, top, z, 0, 0, 1, Cream);
            }
            // The piers either side of the arch, from the springing line to the top.
            if (mid - r > u0) SacredDraw.Quad(m, x, u0, spring, z, mid - r, spring, z, mid - r, top, z, u0, top, z, 0, 0, 1, Cream);
            if (mid + r < u1) SacredDraw.Quad(m, x, mid + r, spring, z, u1, spring, z, u1, top, z, mid + r, top, z, 0, 0, 1, Cream);
        }

        /// <summary>
        /// The painted relief on the gate's core, filling its tall ornate frame (c_westgate/c_23, c_27; c_gje/c_00): on the
        /// forecourt face blue Shiva standing about 2.2 m tall before the snowy peaks of Kailash over a green bank and
        /// light-blue water, the gilt trident with its red ribbon and damaru in his left hand, the water pot in his right,
        /// his hair knot with the crescent moon under a gilt aureole, the black linga at his feet, in a dark carved frame; on
        /// the courtyard face Shiva and Parvati riding the white Nandi in a blue frame.
        /// </summary>
        private static void Relief(MeshData m, in Affine3 x, double v, bool forecourt, int lod)
        {
            const double Hw = 0.88, H = 2.42, Fr = 0.09;
            ShapeBrush frame = forecourt ? PashupatiKit.Br(0x4A3628, MaterialChannel.WoodCarved) : DeepBlue;
            // The frame (a moulded outer and a thin gilt inner line) and the sky.
            SacredDraw.Box(m, x, -Hw - Fr, Hw + Fr, v - Fr, v + H + Fr, 0, 0.07, frame, BoxFaces.Wall);
            if (lod <= 1)
            {
                ShapeBrush line = forecourt ? PashupatiKit.GiltShade : Snow;
                SacredDraw.Box(m, x, -Hw - 0.025, -Hw, v, v + H, 0.07, 0.085, line, BoxFaces.Front);
                SacredDraw.Box(m, x, Hw, Hw + 0.025, v, v + H, 0.07, 0.085, line, BoxFaces.Front);
                SacredDraw.Box(m, x, -Hw, Hw, v + H - 0.025, v + H, 0.07, 0.085, line, BoxFaces.Front);
                SacredDraw.Box(m, x, -Hw, Hw, v, v + 0.025, 0.07, 0.085, line, BoxFaces.Front);
            }
            SacredDraw.Box(m, x, -Hw, Hw, v, v + H, 0, 0.07, forecourt ? SkyBlue : PaleSky, BoxFaces.Front);
            if (lod >= 2) return;
            const double Z = 0.07;
            // Kailash: a row of snowy peaks across the upper half, their shaded flanks blue.
            for (int p = 0; p < 4; p++)
            {
                double cu = -0.66 + 0.44 * p, w = 0.36 + 0.06 * (p % 2), top = v + H - 0.35 - 0.18 * ((p + 1) % 2);
                SacredDraw.Tri(m, x, cu - w, v + 1.15, Z + 0.004, cu + w, v + 1.15, Z + 0.004, cu, top, Z + 0.004, 0, 0, 1, Snow);
                if (lod == 0)
                    SacredDraw.Tri(m, x, cu, v + 1.15, Z + 0.008, cu + w, v + 1.15, Z + 0.008, cu, top, Z + 0.008, 0, 0, 1, SnowShade);
            }
            if (!forecourt)
            {
                ShivaParvati(m, x, v, Z, lod);
                return;
            }
            // The green bank and the water, the black linga on its yoni at Shiva's feet.
            SacredDraw.Box(m, x, -Hw, Hw, v + 0.62, v + 1.15, Z, Z + 0.006, Bank, BoxFaces.Front);
            SacredDraw.Box(m, x, -Hw, Hw, v, v + 0.62, Z, Z + 0.01, Water, BoxFaces.Front);
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.48, v + 0.42, Z + 0.03), PashupatiKit.Ink, 0.22, 0.06, 0.06, lod == 0 ? 10 : 6, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.48, v + 0.56, Z + 0.04), PashupatiKit.Ink, 0.08, 0.13, 0.06, lod == 0 ? 8 : 5, false);
            StandingShiva(m, x, 0.06, v + 0.1, Z + 0.01, 2.2, lod);
        }

        private static readonly ShapeBrush PaleSky = PashupatiKit.Br(0xA9CBE0, MaterialChannel.Paint);
        private static readonly ShapeBrush SnowShade = PashupatiKit.Br(0x8FB2D6, MaterialChannel.Paint);
        private static readonly ShapeBrush Bank = PashupatiKit.Br(0x4E8E4A, MaterialChannel.Paint);
        private static readonly ShapeBrush Dhoti = PashupatiKit.Br(0xE8A08E, MaterialChannel.Paint);
        private static readonly ShapeBrush HairDark = PashupatiKit.Br(0x2E2420, MaterialChannel.Paint);
        private static readonly ShapeBrush SnakeGreen = PashupatiKit.Br(0x3E7A4A, MaterialChannel.Paint);
        private static readonly ShapeBrush Aureole = PashupatiKit.Br(0xF0D27A, MaterialChannel.Gilt);
        private static readonly ShapeBrush Ribbon = PashupatiKit.Br(0xC8302A, MaterialChannel.Paint);

        /// <summary>
        /// Blue Shiva standing in relief (c_westgate/c_23), <paramref name="h"/> tall from his feet at (u, v) on face frame x
        /// (about 6-12 cm proud): legs and feet, the salmon dhoti to the knees with its folds, the slim torso with the green
        /// snake garland and gilt necklace, the right arm down with the water pot, the left raised to the trident, the round
        /// face with dark eyes, the tall dark hair knot with the white crescent, gilt earrings and the gilt aureole.
        /// </summary>
        private static void StandingShiva(MeshData m, in Affine3 x, double u, double v, double z, double h, int lod)
        {
            int seg = lod == 0 ? 10 : 6;
            ShapeBrush skin = ShivaBlue;
            // Aureole behind the head.
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, u, v + 0.86 * h, z + 0.005)), Aureole, 0.13 * h, 0.012, lod == 0 ? 18 : 10, 0, 0,
                            false, true);
            // Legs and feet.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, x, skin, u + sg * 0.05 * h, v + 0.03 * h, z + 0.04, u + sg * 0.055 * h, v + 0.36 * h, z + 0.05, 0.036 * h, seg);
                Shapes.Ellipsoid(m, SacredDraw.At(x, u + sg * 0.06 * h, v + 0.015 * h, z + 0.07), skin, 0.035 * h, 0.018 * h, 0.05, seg, false);
            }
            // The dhoti: a flared skirt from the waist to the knees, the front pleat and a gilt hem.
            PashupatiKit.LatheOf(m, Affine3.Scaling(1, 1, 0.45).Then(SacredDraw.At(x, u, v + 0.34 * h, z + 0.06)), Dhoti,
                                 new[] { 0.0, 0.0, 0.15 * h, 0.0, 0.14 * h, 0.08 * h, 0.115 * h, 0.17 * h, 0.1 * h, 0.22 * h, 0.0, 0.22 * h }, 12, lod, 0b10_0011);
            if (lod == 0)
            {
                Shapes.Bar(m, x, Dhoti, u, v + 0.35 * h, z + 0.12, u + 0.01 * h, v + 0.53 * h, z + 0.11, 0.012 * h, 5);
                Shapes.Torus(m, Affine3.Scaling(1, 1, 0.45).Then(SacredDraw.At(x, u, v + 0.345 * h, z + 0.06)), PashupatiKit.GiltBright, 0.148 * h, 0.006 * h, 14,
                             3);
            }
            // Torso, chest and shoulders.
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.64 * h, z + 0.06), skin, 0.1 * h, 0.125 * h, 0.07, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.74 * h, z + 0.06), skin, 0.13 * h, 0.05 * h, 0.07, seg, false);
            // The snake garland and the gilt necklace.
            Path3 g = PashupatiKit.PathS;
            g.Add(u - 0.08 * h, v + 0.75 * h, z + 0.1).Add(u - 0.05 * h, v + 0.62 * h, z + 0.12).Add(u + 0.02 * h, v + 0.57 * h, z + 0.12)
             .Add(u + 0.07 * h, v + 0.64 * h, z + 0.12).Add(u + 0.08 * h, v + 0.75 * h, z + 0.1);
            Shapes.Tube(m, x, SnakeGreen, g, 0.009 * h, 5, true);
            if (lod == 0) Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, u, v + 0.73 * h, z + 0.11)), PashupatiKit.GiltBright, 0.05 * h, 0.005 * h, 12, 3);
            // Right arm (viewer's left) down to the water pot; left arm (viewer's right) up to the trident.
            Shapes.Bar(m, x, skin, u - 0.12 * h, v + 0.74 * h, z + 0.07, u - 0.15 * h, v + 0.58 * h, z + 0.08, 0.032 * h, seg);
            Shapes.Bar(m, x, skin, u - 0.15 * h, v + 0.58 * h, z + 0.08, u - 0.14 * h, v + 0.45 * h, z + 0.09, 0.028 * h, seg);
            Shapes.Sphere(m, SacredDraw.At(x, u - 0.14 * h, v + 0.41 * h, z + 0.1), PashupatiKit.Brass, 0.035 * h, seg);
            Shapes.Bar(m, x, skin, u + 0.12 * h, v + 0.74 * h, z + 0.07, u + 0.16 * h, v + 0.62 * h, z + 0.08, 0.032 * h, seg);
            Shapes.Bar(m, x, skin, u + 0.16 * h, v + 0.62 * h, z + 0.08, u + 0.17 * h, v + 0.76 * h, z + 0.1, 0.028 * h, seg);
            // The trident: the gilt shaft from the ground past his head, three prongs, the red ribbon and the damaru.
            double tu = u + 0.19 * h;
            Shapes.Bar(m, x, PashupatiKit.GiltBright, tu, v, z + 0.11, tu, v + 1.02 * h, z + 0.11, 0.008 * h, 5);
            Shapes.Bar(m, x, PashupatiKit.GiltBright, tu - 0.05 * h, v + 0.96 * h, z + 0.11, tu + 0.05 * h, v + 0.96 * h, z + 0.11, 0.006 * h, 4);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Path3 pr = PashupatiKit.PathS;
                pr.Add(tu + sg * 0.05 * h, v + 0.96 * h, z + 0.11).Add(tu + sg * 0.055 * h, v + 1.03 * h, z + 0.11).Add(tu + sg * 0.04 * h, v + 1.08 * h, z + 0.11);
                Shapes.Tube(m, x, PashupatiKit.GiltBright, pr, 0.006 * h, 4, true, false, default, 0.002 * h);
            }
            Shapes.Cone(m, SacredDraw.At(x, tu, v + 1.02 * h, z + 0.11), PashupatiKit.GiltBright, 0.012 * h, 0.07 * h, 5, 0, 0, false);
            if (lod == 0)
            {
                Path3 rb = PashupatiKit.PathS;
                rb.Add(tu, v + 0.92 * h, z + 0.12).Add(tu - 0.03 * h, v + 0.86 * h, z + 0.13).Add(tu - 0.02 * h, v + 0.78 * h, z + 0.13).Add(tu - 0.04 * h, v + 0.72 * h, z + 0.12);
                Shapes.Tube(m, x, Ribbon, rb, 0.006 * h, 4, true);
                Shapes.Frustum(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(x, tu + 0.035 * h, v + 0.88 * h, z + 0.12)), PashupatiKit.GiltShade, 0.02 * h,
                               0.006 * h, 0.035 * h, 6, 0, 0, true, false);
            }
            // Head, face and the hair knot with the crescent.
            double hy = v + 0.83 * h;
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, hy, z + 0.08), skin, 0.058 * h, 0.066 * h, 0.06, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, hy + 0.075 * h, z + 0.07), HairDark, 0.04 * h, 0.06 * h, 0.04, seg, false);
            if (lod == 0)
            {
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Shapes.Ellipsoid(m, SacredDraw.At(x, u + sg * 0.018 * h, hy + 0.008 * h, z + 0.125), PashupatiKit.Ink, 0.008 * h, 0.005 * h, 0.006, 5, false);
                    Shapes.Sphere(m, SacredDraw.At(x, u + sg * 0.05 * h, hy - 0.012 * h, z + 0.09), PashupatiKit.GiltBright, 0.008 * h, 4);
                }
                Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, u - 0.025 * h, hy + 0.09 * h, z + 0.11)), Snow, 0.022 * h, 0.005 * h, 8, 3, default, 200,
                             160, true);
            }
        }

        /// <summary>The courtyard face's painting (c_gje/c_00): the white Nandi walking with Shiva (blue, holding his
        /// trident) and Parvati (in a red sari) riding on it, over a green bank.</summary>
        private static void ShivaParvati(MeshData m, in Affine3 x, double v, double z, int lod)
        {
            int seg = lod == 0 ? 10 : 6;
            ShapeBrush bull = PashupatiKit.Br(0xF2EEE4, MaterialChannel.Paint);
            SacredDraw.Box(m, x, -0.88, 0.88, v, v + 0.5, z, z + 0.006, Bank, BoxFaces.Front);
            // The bull: body, hump, head turned out, legs, horns.
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0.05, v + 0.85, z + 0.04), bull, 0.62, 0.26, 0.06, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.42, v + 1.05, z + 0.05), bull, 0.16, 0.14, 0.06, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.66, v + 0.98, z + 0.06), bull, 0.14, 0.17, 0.06, seg, false);
            for (int q = 0; q < 4; q++)
                Shapes.Bar(m, x, bull, -0.38 + 0.26 * q, v + 0.75, z + 0.05, -0.42 + 0.26 * q + 0.05 * (q % 2), v + 0.4, z + 0.05, 0.045, seg);
            Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.72, v + 1.12, z + 0.08, -0.78, v + 1.26, z + 0.08, 0.018, 4);
            Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.6, v + 1.12, z + 0.08, -0.55, v + 1.26, z + 0.08, 0.018, 4);
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0.05, v + 0.98, z + 0.08), Ribbon, 0.3, 0.09, 0.03, seg, false);
            // Shiva in front (blue), Parvati behind (fair, red sari), seated; the trident over his shoulder.
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.1, v + 1.35, z + 0.08), ShivaBlue, 0.14, 0.24, 0.06, seg, false);
            Shapes.Sphere(m, SacredDraw.At(x, -0.1, v + 1.68, z + 0.09), ShivaBlue, 0.09, seg);
            Shapes.Ellipsoid(m, SacredDraw.At(x, -0.1, v + 1.82, z + 0.08), HairDark, 0.06, 0.08, 0.04, seg, false);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, -0.1, v + 1.7, z + 0.03)), Aureole, 0.17, 0.01, 14, 0, 0, false, true);
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0.28, v + 1.3, z + 0.07), Ribbon, 0.14, 0.22, 0.06, seg, false);
            Shapes.Sphere(m, SacredDraw.At(x, 0.28, v + 1.6, z + 0.08), PashupatiKit.Br(0xE8B48A, MaterialChannel.Paint), 0.085, seg);
            Shapes.Cone(m, SacredDraw.At(x, 0.28, v + 1.67, z + 0.08), PashupatiKit.GiltBright, 0.06, 0.12, seg, 0, 0, false);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, 0.28, v + 1.62, z + 0.02)), Aureole, 0.15, 0.01, 14, 0, 0, false, true);
            Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.32, v + 0.95, z + 0.12, -0.36, v + 2.15, z + 0.12, 0.014, 4);
            for (int sg = -1; sg <= 1; sg += 2)
                Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.36, v + 2.05, z + 0.12, -0.36 + sg * 0.08, v + 2.2, z + 0.12, 0.011, 4);
        }

        /// <summary>A tall vine panel at u from v0 to v1 on face frame x: a white field in a thin border, the vase at its foot
        /// and the vine climbing it with leaves and lotus flowers (green and orange on the forecourt face, blue on the
        /// courtyard face).</summary>
        private static void VinePanel(MeshData m, in Affine3 x, double u, double v0, double v1, bool forecourt, int lod)
        {
            const double Hw = 0.3;
            ShapeBrush rim = forecourt ? CreamShade : DeepBlue, leaf = forecourt ? VineGreen : DeepBlue, flower = forecourt ? LotusRed : SkyBlue;
            SacredDraw.Box(m, x, u - Hw - 0.03, u + Hw + 0.03, v0, v1, 0, 0.03, rim, BoxFaces.Front);
            SacredDraw.Box(m, x, u - Hw, u + Hw, v0 + 0.03, v1 - 0.03, 0, 0.04, Snow, BoxFaces.Front);
            if (lod >= 2) return;
            double h = v1 - v0;
            // The vase (blue body, orange pot on the forecourt face).
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v0 + 0.32 * h, 0.06), forecourt ? DeepBlue : SkyBlue, 0.12, 0.17, 0.035, lod == 0 ? 8 : 5, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v0 + 0.44 * h, 0.065), forecourt ? OmOrange : DeepBlue, 0.08, 0.06, 0.03, lod == 0 ? 8 : 5, false);
            Shapes.Bar(m, x, leaf, u, v0 + 0.47 * h, 0.06, u, v1 - 0.12, 0.06, 0.018, 4);
            if (lod > 0) return;
            for (int q = 0; q < 4; q++)
            {
                double ly = v0 + (0.55 + 0.1 * q) * h, sg = q % 2 == 0 ? 1 : -1;
                Path3 p = PashupatiKit.PathS;
                p.Add(u, ly, 0.06).Add(u + sg * 0.08, ly + 0.05, 0.062).Add(u + sg * 0.12, ly + 0.14, 0.062);
                Shapes.Tube(m, x, leaf, p, 0.012, 4, true);
                Shapes.Ellipsoid(m, SacredDraw.At(x, u + sg * 0.12, ly + 0.16, 0.065), q % 2 == 0 ? flower : leaf, 0.05, 0.05, 0.02, 6, false);
            }
            Shapes.Sphere(m, SacredDraw.At(x, u, v0 + 0.14 * h, 0.06), flower, 0.07, 6);
            Shapes.Sphere(m, SacredDraw.At(x, u, v1 - 0.1, 0.06), flower, 0.06, 6);
        }

        /// <summary>
        /// A painted seated deity in relief, <paramref name="h"/> tall from its lotus seat at (u, v) on face frame x: the
        /// aureole behind, crossed legs in a red dhoti, the torso, four arms (the upper pair raised with their attributes,
        /// the lower pair on the knees), the head and a gilt crown. Ganesh (orange-red) has the elephant head: broad fanned
        /// ears and the trunk curling to his left, and the pot belly; the solar deity is golden with a round face.
        /// </summary>
        private static void SeatedDeity(MeshData m, in Affine3 x, double u, double v, double z, double h, bool ganesh, int lod)
        {
            ShapeBrush skin = ganesh ? PashupatiKit.Br(0xE06A2E, MaterialChannel.Paint) : PashupatiKit.Br(0xE3A33A, MaterialChannel.Paint);
            ShapeBrush robe = ganesh ? PashupatiKit.Br(0xC23A2A, MaterialChannel.Paint) : PashupatiKit.Br(0xB8302A, MaterialChannel.Paint);
            ShapeBrush halo = PashupatiKit.Br(0xF0D27A, MaterialChannel.Paint);
            int seg = lod == 0 ? 7 : 4;
            if (lod >= 2)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.45 * h, z), skin, 0.3 * h, 0.45 * h, 0.04, 4, false);
                return;
            }
            // Aureole and the lotus seat.
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.62 * h, z), halo, 0.36 * h, 0.4 * h, 0.015, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.04 * h, z + 0.02), PashupatiKit.Br(0xD0453A, MaterialChannel.Paint), 0.34 * h, 0.05 * h, 0.04, seg, false);
            // Crossed legs, torso (Ganesh's pot belly), shoulders.
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.14 * h, z + 0.04), robe, 0.32 * h, 0.09 * h, 0.07, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + (ganesh ? 0.33 : 0.38) * h, z + 0.05), skin, (ganesh ? 0.19 : 0.15) * h, (ganesh ? 0.19 : 0.2) * h,
                             ganesh ? 0.09 : 0.06, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.53 * h, z + 0.04), skin, 0.17 * h, 0.05 * h, 0.05, seg, false);
            // Arms: the lower pair to the knees, the upper pair raised.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, x, skin, u + sg * 0.15 * h, v + 0.52 * h, z + 0.06, u + sg * 0.22 * h, v + 0.22 * h, z + 0.07, 0.025 * h, 4);
                // The upper pair out from the shoulders and bent up at the elbow, each hand holding an attribute.
                Shapes.Bar(m, x, skin, u + sg * 0.14 * h, v + 0.52 * h, z + 0.05, u + sg * 0.27 * h, v + 0.5 * h, z + 0.05, 0.022 * h, 4);
                Shapes.Bar(m, x, skin, u + sg * 0.27 * h, v + 0.5 * h, z + 0.05, u + sg * 0.3 * h, v + 0.62 * h, z + 0.05, 0.02 * h, 4);
                if (lod == 0) Shapes.Sphere(m, SacredDraw.At(x, u + sg * 0.3 * h, v + 0.66 * h, z + 0.06), sg > 0 ? PashupatiKit.GiltBright : halo, 0.04 * h, 5);
            }
            double hy = v + 0.66 * h;
            if (ganesh)
            {
                Shapes.Sphere(m, SacredDraw.At(x, u, hy, z + 0.06), skin, 0.12 * h, seg);
                for (int sg = -1; sg <= 1; sg += 2)
                    Shapes.Ellipsoid(m, SacredDraw.At(x, u + sg * 0.15 * h, hy - 0.01 * h, z + 0.04), skin, 0.1 * h, 0.12 * h, 0.025, seg, false);
                Path3 tr = PashupatiKit.PathS;
                tr.Add(u, hy - 0.04 * h, z + 0.14).Add(u + 0.01 * h, hy - 0.16 * h, z + 0.15).Add(u + 0.06 * h, hy - 0.26 * h, z + 0.14).Add(u + 0.12 * h, hy - 0.24 * h, z + 0.13);
                Shapes.Tube(m, x, skin, tr, 0.035 * h, 5, true, false, default, 0.018 * h);
                if (lod == 0)
                    for (int sg = -1; sg <= 1; sg += 2) Shapes.Sphere(m, SacredDraw.At(x, u + sg * 0.05 * h, hy + 0.03 * h, z + 0.15), PashupatiKit.Ink, 0.012 * h, 3);
            }
            else
            {
                Shapes.Sphere(m, SacredDraw.At(x, u, hy, z + 0.06), skin, 0.1 * h, seg);
                if (lod == 0)
                    for (int sg = -1; sg <= 1; sg += 2) Shapes.Sphere(m, SacredDraw.At(x, u + sg * 0.035 * h, hy + 0.01 * h, z + 0.15), PashupatiKit.Ink, 0.01 * h, 3);
            }
            Shapes.Cone(m, SacredDraw.At(x, u, hy + (ganesh ? 0.09 : 0.08) * h, z + 0.06), PashupatiKit.GiltBright, 0.08 * h, 0.17 * h, seg, 0, 0, false);
        }

        /// <summary>A painted relief figure (robe, torso, head, crown, arms) of height h standing at (u, v).</summary>
        private static void Figure(MeshData m, in Affine3 x, double u, double v, double z, double h, in ShapeBrush skin, int lod)
        {
            int s = lod == 0 ? 6 : 4;
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.3 * h, z), PashupatiKit.Br(0xF0E4C8, MaterialChannel.Paint), 0.13 * h, 0.3 * h, 0.04, s, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, u, v + 0.66 * h, z + 0.01), skin, 0.12 * h, 0.17 * h, 0.05, s, false);
            Shapes.Sphere(m, SacredDraw.At(x, u, v + 0.88 * h, z + 0.02), skin, 0.07 * h, s);
            Shapes.Cone(m, SacredDraw.At(x, u, v + 0.93 * h, z + 0.02), Scroll, 0.05 * h, 0.1 * h, s, 0, 0, false);
            for (int sg = -1; sg <= 1; sg += 2)
                Shapes.Bar(m, x, skin, u + sg * 0.1 * h, v + 0.72 * h, z + 0.02, u + sg * 0.2 * h, v + 0.5 * h, z + 0.03, 0.025 * h, 4);
        }

        /// <summary>The gate roof: the cream metal hip roof with its red edge trim, the pointed Om gables on both faces, the
        /// square lantern with white lattice panels, its red-edged mini roof and the three gilt finials.</summary>
        private static void GateRoof(MeshData m, in Affine3 g, int lod)
        {
            const double Hw = WestHalfWidth, Hd = WestHalfDepth, Eave = 9.3, Top = 10.55;
            var r = new PRoof
            {
                // The broad, flared cream roof (c_westgate/c_27, c_gje/c_00): deep eaves, concave slopes and lifted corners.
                EaveU = Hw + 1.35, EaveW = Hd + 1.35, EaveV = Eave - 0.12, TopU = 1.85, TopW = 1.85, TopV = Top, Lift = 0.32, FasciaH = 0.22, SeamPitch = 0,
                CurlSize = 0, Flare = 0.9, Roof = RoofCream, Seam = RoofCreamRib, Fascia = PashupatiKit.RedTrim, Curl = PashupatiKit.RedTrim,
            };
            PashupatiKit.Roof(m, g, r, lod);
            PashupatiKit.Skirt(m, g, Hw, Hd, Eave - 0.1, Hw + 1.3, Hd + 1.3, Eave - 0.34, 1, 2);
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 x = FaceFrame(g, face, Hd + 0.9);
                double gw = 1.75, gb = Eave - 0.4, gt = Top + 0.25;
                // An ogee-pointed gable: two curved flanks meeting in a point, blue, with its red trim.
                int n = lod == 0 ? 8 : 4;
                int c = SacredDraw.V(m, x, 0, gb + 0.45, 0.02, 0, 0, 1, DeepBlue);
                int first = m.VertexCount;
                for (int i = 0; i <= 2 * n; i++)
                {
                    double ang = Math.PI * i / (2 * n);
                    double uu = -gw * Math.Cos(ang), vv = i == n ? gt : gb + (gt - gb) * Math.Pow(Math.Sin(ang), 1.6) * (0.75 + 0.25 * Math.Abs(Math.Cos(ang)));
                    SacredDraw.V(m, x, uu, vv, 0.02, 0, 0, 1, DeepBlue);
                }
                for (int i = 0; i < 2 * n; i++) SacredDraw.Tri(m, c, first + i, first + i + 1);
                Path3 p = PashupatiKit.PathS;
                for (int i = 0; i <= 2 * n; i++)
                {
                    double ang = Math.PI * i / (2 * n);
                    double uu = -(gw + 0.08) * Math.Cos(ang), vv = i == n ? gt + 0.1 : gb + (gt + 0.08 - gb) * Math.Pow(Math.Sin(ang), 1.6) * (0.75 + 0.25 * Math.Abs(Math.Cos(ang)));
                    p.Add(uu, vv, 0.06);
                }
                Shapes.Tube(m, x, PashupatiKit.RedTrim, p, 0.07, 4, true);
                if (lod <= 1)
                {
                    // The Om: a glyph of curls and a crescent with its dot, orange on blue.
                    Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, -0.12, gb + 0.75, 0.05)), OmOrange, 0.22, 0.05, 12, 4, default, 0, 300, true);
                    Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, -0.12, gb + 1.15, 0.05)), OmOrange, 0.16, 0.05, 12, 4, default, 180, 260, true);
                    Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, 0.3, gb + 0.9, 0.05)), OmOrange, 0.18, 0.05, 12, 4, default, 270, 270, true);
                    Shapes.Sphere(m, SacredDraw.At(x, 0.28, gb + 1.45, 0.05), OmOrange, 0.07, 5);
                }
            }
            // The lantern.
            const double L = 1.75, L0 = Top - 0.05, L1 = Top + 0.75;
            PashupatiKit.Block(m, g, -L, L, L0, L1, -L, L, Snow);
            if (lod <= 1)
                for (int s = 0; s < 4; s++)
                {
                    double du, dw;
                    PashupatiKit.Out(s, out du, out dw);
                    Affine3 x = SacredDraw.Basis(g, du * L, 0, dw * L, dw, 0, -du, 0, 1, 0);
                    for (int q = -1; q <= 1; q += 2)
                        SacredParts.LatticeWindow(m, x, q * 0.8, 0.5 * (L0 + L1), 0, 1.2, 0.5, PashupatiKit.Br(0xE8E4DA, MaterialChannel.Wood), lod, 4);
                }
            var mini = new PRoof
            {
                EaveU = L + 0.35, EaveW = L + 0.35, EaveV = L1, TopU = 0.95, TopW = 0.7, TopV = L1 + 0.42, Lift = 0.16, FasciaH = 0.14, SeamPitch = 0, CurlSize = 0,
                Roof = RoofCream, Seam = RoofCreamRib, Fascia = PashupatiKit.RedTrim, Curl = PashupatiKit.RedTrim,
            };
            PashupatiKit.Roof(m, g, mini, Math.Max(1, lod));
            PashupatiKit.Block(m, g, -0.85, 0.85, L1 + 0.4, L1 + 0.52, -0.6, 0.6, Gilt);
            // The three gilt bell finials, about 0.9-1.1 m (c_westgate/c_27).
            for (int q = -1; q <= 1; q++) PashupatiKit.BellFinial(m, g, q * 0.62, L1 + 0.52, 0, q == 0 ? 1.12 : 0.92, q == 0 ? 0.22 : 0.18, Gilt, lod);
        }

        // ---------------------------------------------------------------------------------------------------------
        // East Gate and stair
        // ---------------------------------------------------------------------------------------------------------

        public static void East(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod, v0 = m.VertexCount, i0 = m.IndexCount;
            // Gate frame: z toward the river (126.7°, +a), x along −b, y up from the courtyard.
            Affine3 g = f.Facing(EastFrontA - 1.0, 0, EastB, 1, 0);
            const double Hw = EastHalfWidth, D = 1.0, Body = 4.4;
            double footA, footB;
            StairPoint(StairRun, 0, out footA, out footB);
            // The foot is the riverside walk's level (the hand-off to the river front package), not the sampled terrain:
            // PashupatiGround cuts the terrain and its drawn cells under the stair and the walk (DrawnCut).
            double foot = PashupatiGround.WalkM;
            if (lod >= 3)
            {
                PashupatiKit.Block(m, g, -Hw, Hw, 0, Body, -D, D, PaleYellow);
                Shapes.Cone(m, SacredDraw.At(g, 0, Body, 0), Gilt, 0.5, 2.6, 4, 0, 0, false);
                Affine3 sf = f.Facing(EastFrontA, 0, EastB, Math.Cos(StairSkewRad), Math.Sin(StairSkewRad));
                SacredDraw.Quad(m, sf, -StairHalfWidth, 0, 0, StairHalfWidth, 0, 0, StairHalfWidth, foot, StairRun, -StairHalfWidth, foot, StairRun, 0, StairRun, -foot,
                                StairStone);
                SacredDraw.Bake(m, v0, i0, f.Level + foot);
                return;
            }
            // Body with the door passage.
            PashupatiKit.Block(m, g, -Hw, -EastDoorHalf, -0.3, Body, -D, D, PaleYellow);
            PashupatiKit.Block(m, g, EastDoorHalf, Hw, -0.3, Body, -D, D, PaleYellow);
            PashupatiKit.Block(m, g, -EastDoorHalf, EastDoorHalf, 2.75, Body, -D, D, PaleYellow);
            SacredDraw.Box(m, g, -EastDoorHalf, EastDoorHalf, 2.74, 2.75, -D, D, PashupatiKit.Ao(PaleYellow, 0.7f), BoxFaces.Bottom);
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 x = face > 0 ? SacredDraw.At(g, 0, 0, D) : SacredDraw.Basis(g, 0, 0, -D, -1, 0, 0, 0, 1, 0);
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredDraw.Box(m, x, s > 0 ? Hw - 0.4 : -Hw, s > 0 ? Hw : -Hw + 0.4, 0, Body, 0, 0.12, Pilaster, BoxFaces.Wall);
                    SacredDraw.Box(m, x, s > 0 ? EastDoorHalf : -EastDoorHalf - 0.3, s > 0 ? EastDoorHalf + 0.3 : -EastDoorHalf, 0, 3.1, 0, 0.1, PashupatiKit.Br(0x9A968E, MaterialChannel.Stone), BoxFaces.Wall);
                }
                SacredDraw.Box(m, x, -EastDoorHalf, EastDoorHalf, 2.65, 2.85, 0, 0.12, PashupatiKit.WoodDoor, BoxFaces.Wall | BoxFaces.Bottom);
                if (face > 0)
                {
                    SacredDraw.Box(m, x, -EastDoorHalf + 0.03, -EastDoorHalf + 0.07, 0, 2.65, -0.9, 0, PashupatiKit.WoodDoor, BoxFaces.Right | BoxFaces.Front);
                    SacredDraw.Box(m, x, EastDoorHalf - 0.07, EastDoorHalf - 0.03, 0, 2.65, -0.9, 0, PashupatiKit.WoodDoor, BoxFaces.Left | BoxFaces.Front);
                    // The blue lunette with its green rim and the painted Shiva and Parvati.
                    HalfDisc(m, x, 0, 2.9, 0.02, EastDoorHalf + 0.15, DeepBlue, lod);
                    Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(x, 0, 2.9, 0.06)), RimGreen, EastDoorHalf + 0.17, 0.07, lod == 0 ? 20 : 10, 4, default, 90, 180, true);
                    if (lod == 0)
                    {
                        Figure(m, x, -0.28, 2.95, 0.05, 0.75, ShivaBlue, lod);
                        Figure(m, x, 0.3, 2.95, 0.05, 0.68, PashupatiKit.Br(0xE0A070, MaterialChannel.Paint), lod);
                    }
                }
            }
            // Cornice with the red trim, the tall ogee pediment (both faces) carrying the Om roundel, the great gilt bell
            // finial and the two small corner finials (c_gje/c_01, c_04).
            PashupatiKit.Band(m, g, 0, 0, Hw, D, new[] { 0.0, Body - 0.25, 0.1, Body - 0.2, 0.1, Body - 0.05, 0.2, Body }, PashupatiKit.RedTrim, true, PaleYellow);
            Pediment(m, g, D + 0.12, Body, lod);
            Pediment(m, SacredDraw.Basis(g, 0, 0, 0, -1, 0, 0, 0, 1, 0), D + 0.12, Body, lod);
            Affine3 front = SacredDraw.At(g, 0, 0, D + 0.15);
            const double Roundel = Body + 0.72;
            // The roundel (about 0.65 m): a blue rim, a white face and the gilt Om (c_gje/c_01, c_04).
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(front, 0, Roundel, 0.0)), DeepBlue, 0.33, 0.06, lod == 0 ? 16 : 8, 0, 0, false, true);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(front, 0, Roundel, 0.04)), Snow, 0.26, 0.05, lod == 0 ? 16 : 8, 0, 0, false, true);
            if (lod == 0)
            {
                Affine3 om = SacredDraw.At(front, 0, Roundel - 0.16, 0.08);
                Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(om, -0.05, 0.1, 0)), Gilt, 0.075, 0.022, 10, 3, default, 0, 300, true);
                Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(om, -0.05, 0.2, 0)), Gilt, 0.055, 0.022, 10, 3, default, 180, 260, true);
                Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(om, 0.1, 0.14, 0)), Gilt, 0.06, 0.022, 10, 3, default, 270, 270, true);
                Shapes.Sphere(m, SacredDraw.At(om, 0.09, 0.3, 0), Gilt, 0.025, 4);
            }
            // The great gilt bell finial (about 1.7 m, a quarter of the gate's height) on the pediment's apex, and the two
            // corner finials (about 0.6 m) on the cornice.
            PashupatiKit.Block(m, g, -0.45, 0.45, Body + PedimentTop - 0.05, Body + PedimentTop + 0.12, -0.45, 0.45, PaleYellow);
            PashupatiKit.BellFinial(m, g, 0, Body + PedimentTop + 0.12, 0, 1.65, 0.48, Gilt, lod);
            for (int s = -1; s <= 1; s += 2)
            {
                PashupatiKit.Block(m, g, s * (Hw - 0.25) - 0.18, s * (Hw - 0.25) + 0.18, Body, Body + 0.14, 0.32, 0.68, PaleYellow);
                PashupatiKit.BellFinial(m, g, s * (Hw - 0.25), Body + 0.14, 0.5, 0.62, 0.17, Gilt, lod);
            }
            // Two dark stone lions on the landing at the top of the stair, facing the river.
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredParts.Pedestal(m, g, s * 1.45, 0, D + 0.42, 0.25, 0.32, 0.25, PashupatiKit.StoneDark, lod);
                    SacredFigures.Guardian(m, g, s * 1.45, 0.25, D + 0.42, 0.95, GuardianKind.Lion, false, SacredFigures.Small, lod, s);
                }
            Stair(ref f, m, foot, lod);
            SacredDraw.Bake(m, v0, i0, f.Level + foot);

            // Colliders: the gate's two piers, the landing and the stair, the lions.
            double ca = EastFrontA - 1.0;
            f.Box(ca - D, ca + D, EastB + EastDoorHalf, EastB + Hw, -0.3, Body, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.Box(ca - D, ca + D, EastB - Hw, EastB - EastDoorHalf, -0.3, Body, GenColliderFlags.NoClimb, GenColliders.Brick);
            double la, lb;
            StairPoint(Landing, 0, out la, out lb);
            f.Ramp(EastFrontA, EastB, 0, la, lb, 0, StairHalfWidth, GenColliders.Stone);
            f.Ramp(footA, footB, foot, la, lb, 0, StairHalfWidth, GenColliders.Stone);
            for (int s = -1; s <= 1; s += 2)
            {
                double pa, pb;
                StairPoint(0.42, -s * 1.45, out pa, out pb);
                f.Cylinder(pa, pb, 0, 1.2, 0.25);
            }
        }

        /// <summary>The East Gate pediment's apex above the cornice (m): the ogee rises about 1.2 m (c_gje/c_01).</summary>
        private const double PedimentTop = 1.4;

        /// <summary>The landing at the top of the stair, in front of the gate's sill (m along the stair).</summary>
        internal const double Landing = 0.8;

        /// <summary>The stair's risers: about 25 of 0.18 m from the gate's sill down to the riverside walk (pashupati-temple.md
        /// 2.1, c_gje/c_01).</summary>
        public const int StairRisers = 25;

        /// <summary>The stair's run (m) along its skewed axis from the gate front to the ghat walk: it ends where the outer
        /// face of its south wall (the corner nearest the river) meets the walk's edge, so neither the stair's walls nor
        /// its rail stand on the drawn walk.</summary>
        internal static readonly double StairRun = (WalkEdgeA - EastFrontA - (StairHalfWidth + 0.35) * Math.Sin(StairSkewRad)) / Math.Cos(StairSkewRad);

        /// <summary>Site (a, b) of a point z metres down the stair and u across it (u > 0 toward compound south).</summary>
        internal static void StairPoint(double z, double u, out double a, out double b)
        {
            double da = Math.Cos(StairSkewRad), db = Math.Sin(StairSkewRad);
            a = EastFrontA + z * da + u * db;
            b = EastB + z * db - u * da;
        }

        /// <summary>The stair's tread height (above the courtyard) z metres down it: the landing, then the risers.</summary>
        internal static double StairY(double z, double foot)
        {
            if (z <= Landing) return 0;
            return foot * Math.Min(1, (z - Landing) / (StairRun - Landing));
        }

        /// <summary>A flat half disc (the lunette) of radius r on face frame x at (u, v), facing +z.</summary>
        private static void HalfDisc(MeshData m, in Affine3 x, double u, double v, double z, double r, in ShapeBrush b, int lod)
        {
            int n = lod == 0 ? 12 : 6;
            int c = SacredDraw.V(m, x, u, v, z, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI * i / n;
                SacredDraw.V(m, x, u + r * Math.Cos(a), v + r * Math.Sin(a), z, 0, 0, 1, b);
            }
            for (int i = 0; i < n; i++) SacredDraw.Tri(m, c, first + i, first + i + 1);
        }

        /// <summary>The East Gate's ogee pediment on one face (frame z out, at depth z; c_gje/c_01, c_04): a pale panel whose
        /// top rises from the corners in a reversed curve (concave, then convex) about 1.2 m to the finial's pedestal, its
        /// white moulded edge, and the solid back behind it.</summary>
        private static void Pediment(MeshData m, in Affine3 g, double z, double v, int lod)
        {
            int n = lod == 0 ? 10 : 5;
            double hw = EastHalfWidth - 0.2;
            const double Base = 0.2, Rise = PedimentTop - Base, Top = 0.5;
            int first = m.VertexCount;
            for (int i = 0; i <= 2 * n; i++)
            {
                double u = -hw + 2 * hw * i / (2 * n);
                SacredDraw.V(m, g, u, v, z, 0, 0, 1, PaleYellow);
                SacredDraw.V(m, g, u, v + OgeeY(u, hw, Base, Rise, Top), z, 0, 0, 1, PaleYellow);
            }
            for (int i = 0; i < 2 * n; i++) SacredDraw.Quad(m, first + 2 * i, first + 2 * i + 2, first + 2 * i + 3, first + 2 * i + 1);
            Path3 p = PashupatiKit.PathS;
            for (int i = 0; i <= 2 * n; i++)
            {
                double u = -hw + 2 * hw * i / (2 * n);
                p.Add(u, v + OgeeY(u, hw, Base, Rise, Top) + 0.05, z - 0.02);
            }
            Shapes.Tube(m, g, Pilaster, p, 0.07, 4, true);
            PashupatiKit.Block(m, g, -hw, hw, v, v + Base + 0.1, z - 0.8, z - 0.02, PaleYellow);
            PashupatiKit.Block(m, g, -Top, Top, v, v + Base + Rise - 0.05, z - 0.8, z - 0.02, PaleYellow);
        }

        /// <summary>The ogee's height at u over a half width hw: flat at <paramref name="baseH"/> beyond the curve, rising in
        /// a smoothstep (concave from the corners, convex under the top) by <paramref name="rise"/> to the flat top of half
        /// width <paramref name="top"/>.</summary>
        private static double OgeeY(double u, double hw, double baseH, double rise, double top)
        {
            double t = 1 - (Math.Abs(u) - top) / Math.Max(1e-6, hw - top);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return baseH + rise * t * t * (3 - 2 * t);
        }

        /// <summary>The stone stair from the gate down to the riverside walk, in its canyon (c_gje/c_01, c_gje/c_03,
        /// ov_bag/ref_01): the landing at the gate's sill, 25 risers of about 18 cm in dark grey stone as a solid flight
        /// (its sides closed down to the cut ground), on its south side the Mukti block's wall carrying the red-and-white
        /// crenellated parapet of the courtyard strip above, then a white parapet wall with a red band down to the walk,
        /// marigold garlands along its top and the brass bell on its bracket at the top of the stair; the brass handrail
        /// on posts along its north side, the east wing's wall rising beside it.</summary>
        private static void Stair(ref PashupatiFrame f, MeshData m, double foot, int lod)
        {
            double run = StairRun, tread = (run - Landing) / StairRisers, rise = -foot / StairRisers;
            // Stair frame: z down the stair (toward the river, turned 11° toward +b), x across it (about −b), origin at the
            // top's centre.
            Affine3 s = f.Facing(EastFrontA, 0, EastB, Math.Cos(StairSkewRad), Math.Sin(StairSkewRad));
            ShapeBrush riser = PashupatiKit.Ao(StairStone, 0.8f), treadB = PashupatiKit.Br(0x6E6B65, MaterialChannel.Stone);
            double deep = foot - 0.35;
            if (lod >= 2)
            {
                SacredDraw.Quad(m, s, -StairHalfWidth, 0, 0, StairHalfWidth, 0, 0, StairHalfWidth, 0, Landing, -StairHalfWidth, 0, Landing, 0, 1, 0, StairStone);
                SacredDraw.Quad(m, s, -StairHalfWidth, 0, Landing, StairHalfWidth, 0, Landing, StairHalfWidth, foot, run, -StairHalfWidth, foot, run, 0, run, -foot,
                                StairStone);
                SacredDraw.Quad(m, s, -StairHalfWidth, 0, Landing, -StairHalfWidth, foot, run, -StairHalfWidth, deep, run, -StairHalfWidth, deep, Landing, -1, 0, 0,
                                StairStone);
            }
            else
            {
                SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, deep, 0, -0.02, Landing, treadB, BoxFaces.Top | BoxFaces.Left);
                for (int i = 0; i < StairRisers; i++)
                {
                    double top = -i * rise, z0 = Landing + i * tread, z1 = z0 + tread;
                    SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, top - rise, top, z0, z1 + 0.02, treadB, BoxFaces.Top);
                    SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, top - rise, top, z0, z1, riser, BoxFaces.Front);
                    // The flight's north side, closed down to the cut ground.
                    SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, deep, top - rise, z0, z1, riser, BoxFaces.Left);
                }
            }
            // South wall: +x is south (x = −b). Over the Mukti block (to its river face, zM) it rises to the courtyard
            // strip's level and carries its crenellated parapet; beyond it is a parapet wall 1 m above the treads.
            double u0 = StairHalfWidth, u1 = StairHalfWidth + 0.35, zM = MuktiFaceZ;
            int n = lod == 0 ? 12 : 6;
            int iw = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double z = run * i / n, top = z <= zM ? 0.05 : StairY(z, foot) + 1.0, bot = StairY(z, foot) - 0.35;
                SacredDraw.V(m, s, u0, top, z, -1, 0, 0, PashupatiKit.White);
                SacredDraw.V(m, s, u0, bot, z, -1, 0, 0, PashupatiKit.White);
            }
            for (int i = 0; i < n; i++) SacredDraw.Quad(m, iw + 2 * i, iw + 2 * i + 1, iw + 2 * i + 3, iw + 2 * i + 2);
            int ow = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double z = run * i / n, top = z <= zM ? 0.05 : StairY(z, foot) + 1.0, bot = z <= zM ? -0.3 : PashupatiGround.WalkM - 0.3;
                SacredDraw.V(m, s, u1, top, z, 1, 0, 0, PashupatiKit.White);
                SacredDraw.V(m, s, u1, bot, z, 1, 0, 0, PashupatiKit.White);
            }
            for (int i = 0; i < n; i++) SacredDraw.Quad(m, ow + 2 * i, ow + 2 * i + 1, ow + 2 * i + 3, ow + 2 * i + 2);
            int tw = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double z = run * i / n, top = z <= zM ? 0.05 : StairY(z, foot) + 1.0;
                SacredDraw.V(m, s, u0, top, z, 0, 1, 0, PashupatiKit.White);
                SacredDraw.V(m, s, u1, top, z, 0, 1, 0, PashupatiKit.White);
            }
            for (int i = 0; i < n; i++) SacredDraw.Quad(m, tw + 2 * i, tw + 2 * i + 1, tw + 2 * i + 3, tw + 2 * i + 2);
            // The red band along the inner face of the parapet wall.
            int rb = m.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                double z = zM + (run - zM) * i / n, y = StairY(z, foot);
                SacredDraw.V(m, s, u0 - 0.01, y + 0.62, z, -1, 0, 0, PashupatiKit.RedTrim);
                SacredDraw.V(m, s, u0 - 0.01, y + 0.42, z, -1, 0, 0, PashupatiKit.RedTrim);
            }
            for (int i = 0; i < n; i++) SacredDraw.Quad(m, rb + 2 * i, rb + 2 * i + 1, rb + 2 * i + 3, rb + 2 * i + 2);
            // The courtyard strip's parapet over the Mukti block's wall.
            double ca, cb, da2, db2;
            StairPoint(0.05, 0.5 * (u0 + u1), out ca, out cb);
            StairPoint(zM, 0.5 * (u0 + u1), out da2, out db2);
            PashupatiRing.Crenellation(ref f, m, ca, cb, da2, db2, 0.05, lod);
            double wa0, wb0, wa1, wb1;
            StairPoint(zM, StairHalfWidth + 0.17, out wa0, out wb0);
            StairPoint(run, StairHalfWidth + 0.17, out wa1, out wb1);
            f.Wall(wa0, wb0, wa1, wb1, foot, StairY(zM, foot) + 1.0, 0.18);
            if (lod <= 1)
            {
                // The brass handrail on posts along the north side.
                double hu = -StairHalfWidth + 0.1;
                int posts = Math.Max(2, (int)Math.Round((run - Landing) / 1.5));
                for (int q = 0; q <= posts; q++)
                {
                    double z = Landing + (run - Landing) * q / posts;
                    Shapes.Cylinder(m, SacredDraw.At(s, hu, StairY(z, foot), z), PashupatiKit.Brass, 0.025, 0.95, 4, 0, 0, false, true);
                }
                Shapes.Bar(m, s, PashupatiKit.Brass, hu, 0.95, Landing, hu, foot + 0.95, run, 0.03, lod == 0 ? 6 : 4);
                // Marigold garlands looped along the top of the south wall (c_gje/c_01: on the left looking up the stair).
                if (lod == 0)
                {
                    int loops = Math.Max(2, (int)Math.Round((run - zM) / 1.2));
                    double gu = StairHalfWidth + 0.04;
                    for (int q = 0; q < loops; q++)
                    {
                        double za = zM + (run - zM) * q / loops, zb = zM + (run - zM) * (q + 1) / loops;
                        Shapes.Wire(m, s, PashupatiKit.Br(0xF0A020, MaterialChannel.Fabric), gu, StairY(za, foot) + 0.98, za, gu, StairY(zb, foot) + 0.98, zb, 0.3, 0.05,
                                    5, 4);
                    }
                }
                StairPoint(Landing, -StairHalfWidth + 0.1, out wa0, out wb0);
                StairPoint(run, -StairHalfWidth + 0.1, out wa1, out wb1);
                f.Wall(wa0, wb0, wa1, wb1, foot, 0.95, 0.04);
                // The brass bell on its iron bracket out from the south wall at the top of the stair, at head height
                // (c_gje/c_01: top left looking up).
                Affine3 b = SacredDraw.At(s, StairHalfWidth + 0.2, 0, 0.5);
                Shapes.Bar(m, b, PashupatiKit.Ink, 0, 2.7, 0, -0.55, 2.7, 0, 0.03, 4);
                Shapes.Bar(m, b, PashupatiKit.Ink, 0, 0.0, 0, 0.0, 2.75, 0, 0.03, 4);
                PashupatiKit.LatheOf(m, SacredDraw.At(b, -0.5, 2.25, 0), PashupatiKit.Brass,
                                     new[] { 0.0, 0.0, 0.2, 0.0, 0.19, 0.06, 0.15, 0.2, 0.12, 0.36, 0.06, 0.42, 0.0, 0.43 }, 10, lod, 0b10);
            }
        }

        /// <summary>Distance down the stair (m) at which its south wall leaves the Mukti block's river face (a 28.4).</summary>
        private static readonly double MuktiFaceZ = (28.4 - EastFrontA - (StairHalfWidth + 0.17) * Math.Sin(StairSkewRad)) / Math.Cos(StairSkewRad);
    }
}
