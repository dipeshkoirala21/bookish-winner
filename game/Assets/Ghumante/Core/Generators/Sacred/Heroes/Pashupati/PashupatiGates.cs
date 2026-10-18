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
    /// dark door, the blue lunette (Shiva and Parvati) in its green rim, the curved pediment with the clock roundel, a large
    /// gilt bell finial and two small ones, two dark stone lions on the top step; the stone stair (about 25 risers) down to
    /// the ghat walk with a white wall on its south side hung with marigold garlands and the brass bell on its bracket, and
    /// the brass handrail on its north side (c_gje/c_01, c_03, c_04).</item>
    /// </list>
    /// </summary>
    internal static class PashupatiGates
    {
        /// <summary>The West Gate: centre (site a and b), half depth and half width: the OSM gatehouse w913170307 and the
        /// footway through it (w1328801417), 3.6 m south of the temple's door axis; c_westgate/c_26 agrees (from the
        /// threshold the silver west door shows to the right of the Nandi's rump, so the passage is south of the axis).</summary>
        public const double WestA = -36.75, WestB = -3.6, WestHalfDepth = 3.4, WestHalfWidth = 4.8;

        /// <summary>Finial tip above the courtyard (12.5 m above the forecourt [P], which stands 0.6 m higher).</summary>
        public const float WestGateTopM = 13.4f;

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

            // Colliders: the two side bays (never climbed), the passage floor, the steps, the columns.
            double ga = WestA;
            // (Gate frame x runs along +b from the passage centre at WestB.)
            const double B = WestB;
            f.Box(ga - Hd, ga + Hd, B + Op, B + Hw, rise - 0.6, rise + Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.Box(ga - Hd, ga + Hd, B - Hw, B - Op, rise - 0.6, rise + Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            // The passage floor is a level walkway at the forecourt's height (ramps, never a blocking solid): the mapped
            // footway w1328801417 ends in the passage, and the terrain's 8 m grid eases from the forecourt to the courtyard
            // under it. The first piece is the footway's own end (along it, over its drawn end cap). The flight down to
            // the courtyard is a third ramp.
            const double FootwayEnd = -38.6;
            f.Ramp(ga - Hd - 0.2, B, rise, FootwayEnd, B, rise, Op, GenColliders.Stone);
            f.Ramp(FootwayEnd, B, rise, ga + Hd, B, rise, Op, GenColliders.Stone);
            f.Ramp(ga + Hd + 0.96, B, foot, ga + Hd, B, rise, Op, GenColliders.Stone);
            foreach (double cx in ColumnsU)
                for (int face = -1; face <= 1; face += 2)
                    f.Cylinder(ga - face * (Hd + 0.32), B + cx, rise, rise + Ground, 0.32);
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
            Shapes.RoundedBox(m, SacredDraw.At(at, 0, 0.25, 0), ColumnStone, 0.75, 0.5, 0.75, 0.04, 1);
            PashupatiKit.LatheOf(m, at, ColumnStone,
                                 new[]
                                 {
                                     0.0, 0.5, 0.33, 0.5, 0.3, 0.62, 0.275, 0.7, 0.26, h - 0.95, 0.3, h - 0.9, 0.3, h - 0.82, 0.26, h - 0.78, 0.3, h - 0.45, 0.42, h - 0.2,
                                     0.42, h - 0.12, 0.0, h - 0.12,
                                 }, 10, lod, 0b0101_1101_1111);
            if (lod == 0)
                for (int i = 0; i < 6; i++)
                {
                    double a = 2 * Math.PI * i / 6;
                    Shapes.Ellipsoid(m, SacredDraw.At(at, 0.33 * Math.Sin(a), h - 0.45, 0.33 * Math.Cos(a)), ColumnStone, 0.09, 0.16, 0.09, 4, false);
                }
            Shapes.RoundedBox(m, SacredDraw.At(at, 0, h - 0.06, 0), ColumnStone, 0.9, 0.12, 0.9, 0.02, 1);
        }

        /// <summary>The upper storey (c_westgate/c_27, c_gje/c_00): floor slab, the end walls, the solid core carrying the
        /// painted reliefs (both faces) between white vine panels, and either side of it an open loggia through the depth of
        /// the gate: round cream columns at both faces, a wide round arch on each face with its filled spandrels, the
        /// balustrade with its dark scrollwork, and the dark scroll frieze under the cornice.</summary>
        private static void UpperStorey(MeshData m, in Affine3 g, int lod)
        {
            const double Hw = WestHalfWidth, Hd = WestHalfDepth, Floor = 6.3, Eave = 9.3, Core = 1.5, ArchTop = Floor + 2.55, ArchRise = 0.45, Rail = 0.6;
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
                Relief(m, x, Floor + 0.35, face > 0, lod);
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredDraw.Box(m, x, s * 1.1 - 0.22, s * 1.1 + 0.22, Floor + 0.2, Floor + 2.55, 0, 0.04, Snow, BoxFaces.Front);
                    if (lod == 0)
                    {
                        // Vase with a lotus vine climbing from it.
                        Shapes.Ellipsoid(m, SacredDraw.At(x, s * 1.1, Floor + 1.4, 0.07), OmOrange, 0.12, 0.16, 0.04, 6, false);
                        Shapes.Bar(m, x, VineGreen, s * 1.1, Floor + 1.55, 0.06, s * 1.1, Floor + 2.4, 0.06, 0.025, 4);
                        Shapes.Sphere(m, SacredDraw.At(x, s * 1.1, Floor + 0.55, 0.06), LotusRed, 0.07, 5);
                        Shapes.Sphere(m, SacredDraw.At(x, s * 1.1 + 0.08, Floor + 2.1, 0.06), LotusRed, 0.06, 5);
                        for (int q = 0; q < 3; q++)
                        {
                            double ly = Floor + 1.7 + 0.25 * q, lx = s * 1.1 + (q % 2 == 0 ? 0.1 : -0.1);
                            Shapes.Ellipsoid(m, SacredDraw.At(x, lx, ly, 0.065), VineGreen, 0.07, 0.035, 0.015, 4, false);
                        }
                    }
                    // The cream pilaster between the white panel and the loggia.
                    SacredDraw.Box(m, x, s * 1.5 - 0.16, s * 1.5 + 0.16, Floor + 0.1, Eave - 0.4, 0, 0.12, CreamShade, BoxFaces.Wall);
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

        /// <summary>The painted relief on the gate's core: on the forecourt face blue Shiva standing with his trident before
        /// the snowy peaks of Kailash over green water with a linga at his feet; on the courtyard face Shiva and Parvati.</summary>
        private static void Relief(MeshData m, in Affine3 x, double v, bool forecourt, int lod)
        {
            const double Hw = 0.85, H = 2.0;
            SacredDraw.Box(m, x, -Hw - 0.08, Hw + 0.08, v - 0.08, v + H + 0.08, 0, 0.06, PashupatiKit.Br(0x6A4A30, MaterialChannel.WoodCarved), BoxFaces.Wall);
            SacredDraw.Box(m, x, -Hw, Hw, v, v + H, 0, 0.07, SkyBlue, BoxFaces.Front);
            if (lod >= 2) return;
            for (int p = 0; p < 3; p++)
            {
                double cu = -0.55 + 0.55 * p, w = 0.45 + 0.1 * (p % 2);
                SacredDraw.Tri(m, x, cu - w, v + 0.9, 0.075, cu + w, v + 0.9, 0.075, cu, v + 1.75 - 0.15 * (p % 2), 0.075, 0, 0, 1, Snow);
            }
            SacredDraw.Box(m, x, -Hw, Hw, v, v + 0.45, 0.07, 0.08, Water, BoxFaces.Front);
            if (forecourt)
            {
                Figure(m, x, 0.02, v + 0.2, 0.1, 1.68, ShivaBlue, lod);
                Shapes.Bar(m, x, Gilt, 0.4, v + 0.2, 0.18, 0.4, v + H - 0.15, 0.18, 0.025, 4);
                SacredDraw.Tri(m, x, 0.28, v + H - 0.35, 0.19, 0.52, v + H - 0.35, 0.19, 0.4, v + H - 0.05, 0.19, 0, 0, 1, Gilt);
                Shapes.Ellipsoid(m, SacredDraw.At(x, -0.45, v + 0.32, 0.13), PashupatiKit.StoneDark, 0.18, 0.12, 0.08, 6, false);
            }
            else
            {
                // Shiva and Parvati riding the white Nandi (c_gje/c_00).
                ShapeBrush bull = PashupatiKit.Br(0xF2EEE4, MaterialChannel.Paint);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0.05, v + 0.62, 0.1), bull, 0.62, 0.24, 0.05, 8, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, -0.62, v + 0.8, 0.11), bull, 0.16, 0.18, 0.05, 6, false);
                for (int q = 0; q < 4; q++)
                    Shapes.Bar(m, x, bull, -0.4 + 0.27 * q, v + 0.5, 0.11, -0.43 + 0.27 * q + 0.04 * (q % 2), v + 0.12, 0.11, 0.04, 4);
                Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.66, v + 0.94, 0.12, -0.74, v + 1.06, 0.12, 0.018, 3);
                Shapes.Bar(m, x, PashupatiKit.GiltBright, -0.58, v + 0.94, 0.12, -0.52, v + 1.06, 0.12, 0.018, 3);
                Figure(m, x, -0.2, v + 0.75, 0.13, 1.05, ShivaBlue, lod);
                Figure(m, x, 0.3, v + 0.75, 0.13, 0.95, PashupatiKit.Br(0xE0A070, MaterialChannel.Paint), lod);
            }
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
                EaveU = Hw + 0.9, EaveW = Hd + 0.9, EaveV = Eave - 0.05, TopU = 1.85, TopW = 1.85, TopV = Top, Lift = 0.12, FasciaH = 0.22, SeamPitch = 0,
                CurlSize = 0, Roof = RoofCream, Seam = RoofCreamRib, Fascia = PashupatiKit.RedTrim, Curl = PashupatiKit.RedTrim,
            };
            PashupatiKit.Roof(m, g, r, lod);
            PashupatiKit.Skirt(m, g, Hw, Hd, Eave - 0.1, Hw + 0.85, Hd + 0.85, Eave - 0.27, 1, 2);
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 x = FaceFrame(g, face, Hd + 0.55);
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
            for (int q = -1; q <= 1; q++) PashupatiKit.BellFinial(m, g, q * 0.62, L1 + 0.52, 0, q == 0 ? 1.0 : 0.75, q == 0 ? 0.21 : 0.15, Gilt, lod);
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
            double foot = Math.Min(-2.5, f.GroundAt(footA, footB, -4.2));
            int steps = Math.Max(8, (int)Math.Round(-foot / 0.18));
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
            // Cornice with the red trim, the curved pediment (both faces) with its clock roundel, the finials.
            PashupatiKit.Band(m, g, 0, 0, Hw, D, new[] { 0.0, Body - 0.25, 0.1, Body - 0.2, 0.1, Body - 0.05, 0.2, Body }, PashupatiKit.RedTrim, true, PaleYellow);
            Pediment(m, g, D + 0.12, Body, lod);
            Pediment(m, SacredDraw.Basis(g, 0, 0, 0, -1, 0, 0, 0, 1, 0), D + 0.12, Body, lod);
            Affine3 front = SacredDraw.At(g, 0, 0, D + 0.15);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(front, 0, Body + 0.75, 0.0)), DeepBlue, 0.36, 0.06, lod == 0 ? 16 : 8, 0, 0, false, true);
            Shapes.Cylinder(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(front, 0, Body + 0.75, 0.04)), Snow, 0.28, 0.05, lod == 0 ? 16 : 8, 0, 0, false, true);
            if (lod == 0)
            {
                Shapes.Bar(m, front, PashupatiKit.Ink, 0, Body + 0.75, 0.1, 0, Body + 0.95, 0.1, 0.015, 3);
                Shapes.Bar(m, front, PashupatiKit.Ink, 0, Body + 0.75, 0.1, 0.13, Body + 0.75, 0.1, 0.015, 3);
            }
            PashupatiKit.Block(m, g, -0.45, 0.45, Body + 1.25, Body + 1.45, -0.45, 0.45, PaleYellow);
            PashupatiKit.BellFinial(m, g, 0, Body + 1.45, 0, 1.45, 0.42, Gilt, lod);
            for (int s = -1; s <= 1; s += 2) PashupatiKit.BellFinial(m, g, s * (Hw - 0.25), Body, 0.5, 0.75, 0.2, Gilt, lod);
            // Two dark stone lions on the top step, facing the river.
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredParts.Pedestal(m, g, s * (StairHalfWidth + 0.3), 0, D + 0.45, 0.25, 0.32, 0.25, PashupatiKit.StoneDark, lod);
                    SacredFigures.Guardian(m, g, s * (StairHalfWidth + 0.3), 0.25, D + 0.45, 0.95, GuardianKind.Lion, false, SacredFigures.Small, lod, s);
                }
            Stair(ref f, m, foot, steps, lod);
            SacredDraw.Bake(m, v0, i0, f.Level + foot);

            // Colliders: the gate's two piers, the stair.
            double ca = EastFrontA - 1.0;
            f.Box(ca - D, ca + D, EastB + EastDoorHalf, EastB + Hw, -0.3, Body, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.Box(ca - D, ca + D, EastB - Hw, EastB - EastDoorHalf, -0.3, Body, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.Ramp(footA, footB, foot, EastFrontA, EastB, 0, StairHalfWidth, GenColliders.Stone);
        }

        /// <summary>The stair's run (m) along its skewed axis from the gate front to the ghat walk: it ends where the outer
        /// face of its south wall (the corner nearest the river) meets the walk's edge, so neither the stair's walls nor
        /// its rail stand on the drawn walk.</summary>
        private static readonly double StairRun = (WalkEdgeA - EastFrontA - (StairHalfWidth + 0.35) * Math.Sin(StairSkewRad)) / Math.Cos(StairSkewRad);

        /// <summary>Site (a, b) of a point z metres down the stair and u across it (u > 0 toward compound south).</summary>
        private static void StairPoint(double z, double u, out double a, out double b)
        {
            double da = Math.Cos(StairSkewRad), db = Math.Sin(StairSkewRad);
            a = EastFrontA + z * da + u * db;
            b = EastB + z * db - u * da;
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

        /// <summary>The East Gate's curved pediment on one face (frame z out, at depth z): a pale panel whose top sweeps up
        /// in concave curves to the finial's pedestal, its white moulded edge, and the solid back behind it.</summary>
        private static void Pediment(MeshData m, in Affine3 g, double z, double v, int lod)
        {
            int n = lod == 0 ? 8 : 4;
            double hw = EastHalfWidth - 0.2;
            int first = m.VertexCount;
            for (int i = 0; i <= 2 * n; i++)
            {
                double t = (double)i / (2 * n), u = -hw + 2 * hw * t, k = 1 - Math.Abs(2 * t - 1);
                SacredDraw.V(m, g, u, v, z, 0, 0, 1, PaleYellow);
                SacredDraw.V(m, g, u, v + 0.25 + 1.0 * Math.Pow(k, 2.2), z, 0, 0, 1, PaleYellow);
            }
            for (int i = 0; i < 2 * n; i++) SacredDraw.Quad(m, first + 2 * i, first + 2 * i + 2, first + 2 * i + 3, first + 2 * i + 1);
            Path3 p = PashupatiKit.PathS;
            for (int i = 0; i <= 2 * n; i++)
            {
                double t = (double)i / (2 * n), u = -hw + 2 * hw * t, k = 1 - Math.Abs(2 * t - 1);
                p.Add(u, v + 0.3 + 1.0 * Math.Pow(k, 2.2), z - 0.02);
            }
            Shapes.Tube(m, g, Pilaster, p, 0.07, 4, true);
            PashupatiKit.Block(m, g, -hw, hw, v, v + 0.3, z - 0.8, z - 0.02, PaleYellow);
        }

        /// <summary>The stone stair from the gate down to the ghat walk: risers of about 18 cm in dark grey stone, a white
        /// wall with a red band on its south side with marigold garlands along its top and the brass bell on its bracket at the
        /// top of the stair, and the brass handrail on its north side.</summary>
        private static void Stair(ref PashupatiFrame f, MeshData m, double foot, int steps, int lod)
        {
            double run = StairRun, tread = run / steps, rise = -foot / steps;
            // Stair frame: z down the stair (toward the river, turned 11° toward +b), x across it (about −b), origin at the
            // top's centre.
            Affine3 s = f.Facing(EastFrontA, 0, EastB, Math.Cos(StairSkewRad), Math.Sin(StairSkewRad));
            ShapeBrush riser = PashupatiKit.Ao(StairStone, 0.8f), treadB = PashupatiKit.Br(0x6E6B65, MaterialChannel.Stone);
            if (lod >= 2)
            {
                SacredDraw.Quad(m, s, -StairHalfWidth, 0, 0, StairHalfWidth, 0, 0, StairHalfWidth, foot, run, -StairHalfWidth, foot, run, 0, run, -foot, StairStone);
            }
            else
            {
                for (int i = 0; i < steps; i++)
                {
                    double top = -i * rise, z0 = i * tread, z1 = (i + 1) * tread;
                    SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, top - rise, top, z0, z1 + 0.02, treadB, BoxFaces.Top);
                    SacredDraw.Box(m, s, -StairHalfWidth, StairHalfWidth, top - rise, top, z0, z1, riser, BoxFaces.Front);
                }
            }
            // South wall (white, red band): +x is south (x = −b).
            double u0 = StairHalfWidth, u1 = StairHalfWidth + 0.35;
            SacredDraw.Quad(m, s, u1, 1.0, 0, u1, 1.0 + foot, run, u1, foot - 0.3, run, u1, -0.3, 0, 1, 0, 0, PashupatiKit.White);
            SacredDraw.Quad(m, s, u0, 1.0, 0, u0, 1.0 + foot, run, u0, foot, run, u0, 0, 0, -1, 0, 0, PashupatiKit.White);
            SacredDraw.Quad(m, s, u0, 1.0, 0, u1, 1.0, 0, u1, 1.0 + foot, run, u0, 1.0 + foot, run, 0, 1, -foot / run, PashupatiKit.White);
            SacredDraw.Quad(m, s, u0 - 0.01, 0.8, 0, u0 - 0.01, 0.8 + foot, run, u0 - 0.01, 0.6 + foot, run, u0 - 0.01, 0.6, 0, -1, 0, 0, PashupatiKit.RedTrim);
            double wa0, wb0, wa1, wb1;
            StairPoint(0, StairHalfWidth + 0.17, out wa0, out wb0);
            StairPoint(run, StairHalfWidth + 0.17, out wa1, out wb1);
            f.Wall(wa0, wb0, wa1, wb1, foot, 1.0, 0.18);
            if (lod <= 1)
            {
                // The brass handrail on posts along the north side.
                double hu = -StairHalfWidth + 0.1;
                int posts = Math.Max(2, (int)Math.Round(run / 1.5));
                for (int q = 0; q <= posts; q++)
                {
                    double z = run * q / posts, y = foot * z / run;
                    Shapes.Cylinder(m, SacredDraw.At(s, hu, y, z), PashupatiKit.Brass, 0.025, 0.95, 4, 0, 0, false, true);
                }
                Shapes.Bar(m, s, PashupatiKit.Brass, hu, 0.95, 0, hu, foot + 0.95, run, 0.03, lod == 0 ? 6 : 4);
                // Marigold garlands looped along the top of the south wall (c_gje/c_01: on the left looking up the stair).
                if (lod == 0)
                {
                    int loops = Math.Max(2, (int)Math.Round(run / 1.2));
                    double gu = StairHalfWidth + 0.04;
                    for (int q = 0; q < loops; q++)
                    {
                        double za = run * q / loops, zb = run * (q + 1) / loops;
                        Shapes.Wire(m, s, PashupatiKit.Br(0xF0A020, MaterialChannel.Fabric), gu, 0.98 + foot * za / run, za, gu, 0.98 + foot * zb / run, zb, 0.3, 0.05,
                                    5, 4);
                    }
                }
                StairPoint(0, -StairHalfWidth + 0.1, out wa0, out wb0);
                StairPoint(run, -StairHalfWidth + 0.1, out wa1, out wb1);
                f.Wall(wa0, wb0, wa1, wb1, foot, 0.95, 0.04);
                // The brass bell on its iron bracket out from the south wall at the top of the stair, at head height
                // (c_gje/c_01: top left looking up).
                Affine3 b = SacredDraw.At(s, StairHalfWidth + 0.2, 0, 0.5);
                Shapes.Bar(m, b, PashupatiKit.Ink, 0, 2.7, 0, -0.55, 2.7, 0, 0.03, 4);
                Shapes.Bar(m, b, PashupatiKit.Ink, 0, 1.0, 0, 0.0, 2.75, 0, 0.03, 4);
                PashupatiKit.LatheOf(m, SacredDraw.At(b, -0.5, 2.25, 0), PashupatiKit.Brass,
                                     new[] { 0.0, 0.0, 0.2, 0.0, 0.19, 0.06, 0.15, 0.2, 0.12, 0.36, 0.06, 0.42, 0.0, 0.43 }, 10, lod, 0b10);
            }
        }
    }
}
