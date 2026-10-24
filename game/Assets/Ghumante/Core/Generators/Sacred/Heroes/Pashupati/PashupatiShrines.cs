using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// The measured section of one of the courtyard's small two-tier Newar temples in its own frame (u, w half sizes
    /// along its walls, heights above the courtyard): plinth, ground-storey wall and cornice, lower eave and the line
    /// where the lower roof meets the upper body, upper body, upper eave, flat top and finial, the roof metal and the
    /// door side (sides as <see cref="PashupatiKit.Out"/>).
    /// </summary>
    internal struct TwoTierSpec
    {
        public double PlinthU, PlinthW, PlinthH;
        public double WallU, WallW, WallTop, CorniceTop;
        public double Eave1U, Eave1W, Eave1V, Top1V;
        public double BodyU, BodyW, BodyTop;
        public double Eave2U, Eave2W, Eave2V, TopV, TopHalf;
        public double FinialH, FinialR, Lift, Curl, SeamPitch;
        public int Struts1, Struts2, DoorSide;
        public ShapeBrush Roof, Rib, Fascia, CurlB, Wall, Body;
        public uint Seed;
    }

    /// <summary>
    /// The temples, shrines, statues and furniture of the Pashupatinath courtyard other than the main temple
    /// (pashupati-temple.md PT-06, PT-07, PT-09, PT-10, PT-14, PT-15, PT-16), each modelled to its OSM square or the
    /// photo position: the verdigris two-tier Vasuki temple (c_inner/c_07), the slate-grey two-tier Bhairav temple, the
    /// octagonal three-roof grey temple with its gilt onion finial, the white mini-shikharas and the bell shrine of the
    /// north-east group, the red pyramidal shikhara (c_inner/c_01, c_gje/c_03), the maroon fluted-dome shrines
    /// (pagoda/ref_02), the grey stone linga shrines before the red wall with its iron trident railing (c_inner/c_00),
    /// the kings' pillars with their gilt kneeling figures under naga hoods (c_inner/c_01), the Dharmashila and the
    /// Chandeshvar linga, the great gilt trident (c_gje/c_00), the stone lamp pillar and the bell frames, and the two
    /// columnar Araucarias that rise behind the temple in the east-bank views. Doors stay closed.
    /// </summary>
    internal static class PashupatiShrines
    {
        /// <summary>Tops above the courtyard: the octagonal temple's onion finial and the Araucarias.</summary>
        public const float OctagonTopM = 12.5f, AraucariaTopM = 36.5f;

        /// <summary>Vasuki temple (OSM w1094633492, 1.2 m west of the mapped square so its broad lower eaves clear the east
        /// wing wall, which the photos show close behind it), turned 7.4° with the square [O].</summary>
        public const double VasukiA = 14.2, VasukiB = 21.0, VasukiYaw = 0.129;

        /// <summary>Bhairav temple (OSM w913170316, its 9.4 × 10.0 m roof outline).</summary>
        public const double BhairavA = 4.9, BhairavB = -17.45, BhairavYaw = 0.022;

        /// <summary>Octagonal three-roof temple (OSM w256799892 and its parts).</summary>
        public const double OctagonA = 3.4, OctagonB = -30.0;

        /// <summary>The five other shrine squares of the north-east group (a, b, yaw): w1094633491, -493, -496,
        /// w1368994740, -741 [O].</summary>
        private static readonly double[] Squares = { 6.9, 17.6, 0.086, 1.4, 21.3, 0.061, 9.9, 28.9, 0.094, 5.7, 28.5, 0.12, 1.4, 28.0, 0.12 };

        /// <summary>Shikhara heights of the squares (the second is the bell shrine).</summary>
        private static readonly double[] SquareHeights = { 5.4, 0, 4.6, 5.0, 4.3 };

        private static readonly ShapeBrush VerdigrisFascia = PashupatiKit.Br(0x4E6A5E, MaterialChannel.Metal);
        private static readonly ShapeBrush SlateFascia = PashupatiKit.Br(0x474D50, MaterialChannel.Metal);
        private static readonly ShapeBrush OctRoof = PashupatiKit.Br(0x6E7477, MaterialChannel.Metal);
        private static readonly ShapeBrush OctRib = PashupatiKit.Br(0x8A9093, MaterialChannel.Metal);
        private static readonly ShapeBrush OctFascia = PashupatiKit.Br(0x50565A, MaterialChannel.Metal);
        private static readonly ShapeBrush GlazedWall = PashupatiKit.Br(0x6A3428, MaterialChannel.BrickGlazed);
        private static readonly ShapeBrush BrickWall = PashupatiKit.Br(0xA4553A, MaterialChannel.Brick);
        private static readonly ShapeBrush Flag = PashupatiKit.Br(0x9A958A, MaterialChannel.Flagstone);
        private static readonly ShapeBrush Maroon = PashupatiKit.Br(0x93392F, MaterialChannel.Paint);
        private static readonly ShapeBrush MaroonDark = PashupatiKit.Br(0x6E2A24, MaterialChannel.Paint);
        private static readonly ShapeBrush RedPlaster = PashupatiKit.Br(0xA8504A, MaterialChannel.Plaster);
        private static readonly ShapeBrush RedPlasterDark = PashupatiKit.Br(0x8A3E38, MaterialChannel.Plaster);
        private static readonly ShapeBrush WallRed = PashupatiKit.Br(0xB02A26, MaterialChannel.Paint);
        private static readonly ShapeBrush Iron = PashupatiKit.Br(0x1A1818, MaterialChannel.Metal);
        private static readonly ShapeBrush WhiteShade = PashupatiKit.Ao(PashupatiKit.White, 0.8f);
        private static readonly ShapeBrush Bark = PashupatiKit.Br(0x5A4636, MaterialChannel.Bark);
        private static readonly ShapeBrush Needles = PashupatiKit.Br(0x2E4A2E, MaterialChannel.Foliage);
        private static readonly ShapeBrush NeedlesLit = PashupatiKit.Br(0x3D5C36, MaterialChannel.Foliage);
        private static readonly ShapeBrush Marigold = PashupatiKit.Br(0xE8902A, MaterialChannel.Foliage);

        [ThreadStatic] private static double[] _pu, _pw, _su, _sw;

        private static void Scratch()
        {
            if (_pu != null) return;
            _pu = new double[32];
            _pw = new double[32];
            _su = new double[32];
            _sw = new double[32];
        }

        /// <summary>A frame at site (a, y, b) turned by <paramref name="yaw"/> (radians, from a toward b): local u along
        /// the turned a axis, w along the turned b axis.</summary>
        private static Affine3 Yawed(ref PashupatiFrame f, double a, double y, double b, double yaw)
        {
            return f.Facing(a, y, b, -Math.Sin(yaw), Math.Cos(yaw));
        }

        /// <summary>The outward face frame of side s of a local rectangle (x along the face, z outward).</summary>
        private static Affine3 Face(in Affine3 k, int s, double hu, double hw)
        {
            double du, dw;
            PashupatiKit.Out(s, out du, out dw);
            double off = s % 2 == 0 ? hw : hu;
            return SacredDraw.Basis(k, du * off, 0, dw * off, dw, 0, -du, 0, 1, 0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Temple part: the furniture round the main temple
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>PT-16 by the main temple: the stone lamp pillar on the path from the East Gate to the east door
        /// (c_gje/c_03) and two brass bells on their stone frames at the temple's north-west and south-east corners.</summary>
        public static void CourtyardFurniture(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            // The lamp pillar on its stone base.
            Affine3 lp = f.At(16.0, 0, 3.5);
            PashupatiKit.Block(m, lp, -0.45, 0.45, -0.1, 0.22, -0.45, 0.45, PashupatiKit.StoneEdge);
            SacredFigures.LampPillar(m, lp, 0, 0.22, 0, 2.5, lod);
            f.Cylinder(16.0, 3.5, 0, 2.7, 0.4);
            if (lod <= 1)
            {
                // Bell frames, the beam along the temple's sides.
                Bell(ref f, m, -11.6, 9.9, 0.42, lod);
                Bell(ref f, m, 11.7, -10.2, 0.42, lod);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        private static void Bell(ref PashupatiFrame f, MeshData m, double a, double b, double d, int lod)
        {
            Affine3 k = f.Facing(a, 0, b, 0, 1);
            SacredFigures.TempleBell(m, k, 0, 0, 0, d, false, lod);
            double span = 1.6 * d + 0.5, hgt = 1.6 * d + 1.4;
            f.Cylinder(a - 0.5 * span, b, 0, hgt, 0.22);
            f.Cylinder(a + 0.5 * span, b, 0, hgt, 0.22);
        }

        // ---------------------------------------------------------------------------------------------------------
        // West Gate part: the great trident
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// PT-15, the great gilt trident (c_gje/c_00) on its dark stone pillar inside the West Gate, north of the
        /// passage: a moulded base and shaft, and the trident about 1.7 m across, its curved side prongs and the spear
        /// point, a damaru on the shaft; its face toward the temple. Top ≈ 4.6 m.
        /// </summary>
        public static void GreatTrident(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            const double A = -26.0, B = 6.0, P = 2.35;
            // z toward the temple (+a), x along −b.
            Affine3 x = f.Facing(A, 0, B, 1, 0);
            PashupatiKit.Band(m, x, 0, 0, 0.55, 0.55, lod >= 2 ? new[] { 0.0, -0.1, 0.0, 0.42 } : new[] { 0.04, -0.1, 0.04, 0.08, 0.0, 0.12, 0.0, 0.36, 0.04, 0.39, 0.04, 0.42 },
                              PashupatiKit.StoneDark, true, PashupatiKit.StoneDark);
            PashupatiKit.LatheOf(m, SacredDraw.At(x, 0, 0.42, 0), PashupatiKit.StoneDark,
                                 new[] { 0.0, 0.0, 0.3, 0.0, 0.3, 0.12, 0.22, 0.2, 0.2, 0.26, 0.18, P - 0.62, 0.24, P - 0.56, 0.24, P - 0.5, 0.0, P - 0.5 }, 8, lod,
                                 0b1_1110_0110);
            ShapeBrush gold = PashupatiKit.GiltBright, shade = PashupatiKit.GiltRoof;
            double s0 = P - 0.08, cross = P + 0.85;
            int rs = lod == 0 ? 6 : 4;
            Shapes.Bar(m, x, gold, 0, s0, 0, 0, cross, 0, 0.055, rs);
            if (lod <= 1)
            {
                // The damaru (two cones tip to tip) on the shaft.
                Shapes.Frustum(m, SacredDraw.At(x, 0, s0 + 0.3, 0), shade, 0.13, 0.03, 0.13, rs, 0, 0, true, false);
                Shapes.Frustum(m, SacredDraw.At(x, 0, s0 + 0.43, 0), shade, 0.03, 0.13, 0.13, rs, 0, 0, false, true);
            }
            // The curved side prongs from the cross piece.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Path3 p = PashupatiKit.PathS;
                p.Add(0, cross - 0.05, 0).Add(sg * 0.3, cross + 0.02, 0).Add(sg * 0.62, cross + 0.18, 0).Add(sg * 0.8, cross + 0.5, 0).Add(sg * 0.84, cross + 0.95, 0)
                 .Add(sg * 0.8, cross + 1.25, 0);
                Shapes.Tube(m, x, gold, p, 0.06, rs, true, false, default, 0.02);
                if (lod == 0) Shapes.Cone(m, SacredDraw.At(x, sg * 0.8, cross + 1.22, 0), gold, 0.05, 0.22, 5, 0, 0, false);
            }
            // The centre prong with its spear point (a flattened lathe).
            Shapes.Bar(m, x, gold, 0, cross - 0.05, 0, 0, cross + 1.0, 0, 0.055, rs);
            Affine3 tip = Affine3.Scaling(1, 1, 0.35).Then(SacredDraw.At(x, 0, cross + 0.95, 0));
            PashupatiKit.LatheOf(m, tip, gold, new[] { 0.0, 0.0, 0.14, 0.12, 0.12, 0.28, 0.0, 0.48 }, 6, lod);
            SacredDraw.Bake(m, v0, i0, f.Level);
            f.Cylinder(A, B, 0, P, 0.5);
        }

        // ---------------------------------------------------------------------------------------------------------
        // South court
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>PT-07, the Bhairav temple (w913170316): a two-tier pagoda of dark slate-grey metal roofs with curled
        /// tips and a gilt bell finial, red brick ground storey with its door toward the main temple, carved struts and
        /// lattice skirts, dark glazed-brick upper body; top ≈ 11.3 m.</summary>
        public static void Bhairav(ref PashupatiFrame f, MeshData m)
        {
            var p = new TwoTierSpec
            {
                PlinthU = 3.5, PlinthW = 3.7, PlinthH = 0.6, WallU = 2.5, WallW = 2.7, WallTop = 4.0, CorniceTop = 4.3, Eave1U = 4.45, Eave1W = 4.7, Eave1V = 5.6,
                Top1V = 7.1, BodyU = 1.85, BodyW = 2.0, BodyTop = 7.85, Eave2U = 3.05, Eave2W = 3.2, Eave2V = 8.75, TopV = 10.2, TopHalf = 0.5, FinialH = 1.15,
                FinialR = 0.36, Lift = 0.18, Curl = 0.4, SeamPitch = 0.85, Struts1 = 4, Struts2 = 4, DoorSide = 0, Roof = PashupatiKit.SlateMetal,
                Rib = PashupatiKit.SlateRib, Fascia = SlateFascia, CurlB = PashupatiKit.SlateRib, Wall = BrickWall, Body = GlazedWall, Seed = 401u,
            };
            int v0 = m.VertexCount, i0 = m.IndexCount;
            TwoTier(ref f, m, BhairavA, BhairavB, BhairavYaw, p);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>
        /// PT-09, the grey octagonal three-roof temple of the south court (w256799892 and its parts): a stone octagonal
        /// plinth to the OSM outline (7.1 m), a dark glazed-brick octagonal body with its door toward the court, and three
        /// stepped grey metal roofs (eaves about 6.7, 5.5 and 4.4 m across at 4.5, 7.4 and 10.3 m) over black lattice
        /// skirts and struts, under the gilt onion finial to 12.5 m.
        /// </summary>
        public static void Octagon(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod, v0 = m.VertexCount, i0 = m.IndexCount;
            Scratch();
            const double Rot = Math.PI / 8, Fin = 11.15;
            const int Sides = 8;
            Affine3 k = f.At(OctagonA, 0, OctagonB);
            if (lod >= 3)
            {
                Shapes.Frustum(m, SacredDraw.At(k, 0, -0.25, 0), GlazedWall, 2.3, 1.2, 9.95, 6, 0, 0, false, false);
                Shapes.Frustum(m, SacredDraw.At(k, 0, 4.4, 0), OctRoof, 3.35, 1.7, 1.0, 6, 0, 0, true, false);
                Shapes.Frustum(m, SacredDraw.At(k, 0, 7.3, 0), OctRoof, 2.75, 1.25, 0.9, 6, 0, 0, true, false);
                Shapes.Cone(m, SacredDraw.At(k, 0, 10.2, 0), OctRoof, 2.2, 1.0, 6, 0, 0, true);
                Shapes.Cone(m, SacredDraw.At(k, 0, Fin, 0), PashupatiKit.GiltBright, 0.35, OctagonTopM - Fin, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            // Plinth.
            int n = Poly(Sides, 3.55, Rot, _pu, _pw);
            Mould pl = SacredDraw.M;
            if (lod >= 2) pl.Add(0, -0.25, PashupatiKit.StonePlinth).Add(0, 0.5, PashupatiKit.StoneEdge);
            else
                pl.Add(0.04, -0.25, PashupatiKit.StoneDark).Add(0.04, 0.1).Add(0, 0.14, PashupatiKit.StonePlinth).Add(0, 0.42).Add(0.05, 0.46, PashupatiKit.StoneEdge)
                  .Add(0.05, 0.5);
            SacredDraw.Ring(m, k, _pu, _pw, n, pl);
            SacredDraw.Cap(m, k, _pu, _pw, n, pl.O[pl.Count - 1], 0.5, true, Flag);
            // A step up to the door (north, toward the court).
            Affine3 door = SacredDraw.Basis(k, 0, 0, 3.55 * Math.Cos(Math.PI / Sides), 1, 0, 0, 0, 1, 0);
            SacredDraw.Box(m, door, -0.7, 0.7, 0, 0.25, -0.02, 0.32, PashupatiKit.StoneEdge, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
            // Three storeys of body, skirt, struts and roof.
            double[] bodyR = { 2.35, 1.6, 1.15 }, bodyV0 = { 0.5, 5.05, 7.95 }, bodyV1 = { 3.95, 6.75, 9.6 };
            double[] eaveR = { 3.35, 2.75, 2.2 }, eaveV = { 4.5, 7.4, 10.3 }, topR = { 1.72, 1.22, 0.26 }, topV = { 5.35, 8.2, Fin };
            for (int t = 0; t < 3; t++)
            {
                n = Poly(Sides, bodyR[t], Rot, _pu, _pw);
                Mould p = SacredDraw.M;
                p.Add(0, bodyV0[t], t == 0 ? GlazedWall : PashupatiKit.WoodDark).Add(0, bodyV1[t] - 0.3, PashupatiKit.WoodDark).Add(0.1, bodyV1[t] - 0.25)
                 .Add(0.1, bodyV1[t]);
                SacredDraw.Ring(m, k, _pu, _pw, n, p);
                if (t == 0 || lod == 0)
                {
                    // Faces at 0°, 90°, 180° and 270°: the door (north) and lattice windows.
                    double ap = bodyR[t] * Math.Cos(Math.PI / Sides);
                    for (int s = 0; s < Sides; s += 2)
                    {
                        double th = 2 * Math.PI * s / Sides;
                        Affine3 x = SacredDraw.Basis(k, ap * Math.Cos(th), 0, ap * Math.Sin(th), Math.Sin(th), 0, -Math.Cos(th), 0, 1, 0);
                        if (t == 0 && s == 2) SacredParts.Door(m, x, 0, 0.5, 0, 0.9, 1.9, PashupatiKit.WoodDark, lod, true, bodyV1[0] - 0.32);
                        else if (t == 0 && lod <= 1) SacredParts.LatticeWindow(m, x, 0, 2.2, 0, 0.6, 0.75, PashupatiKit.WoodDark, lod, 3);
                        else if (t > 0) SacredParts.LatticeWindow(m, x, 0, 0.5 * (bodyV0[t] + bodyV1[t]) + 0.15, 0, 0.42, 0.3, PashupatiKit.WoodDark, lod, 2);
                    }
                }
                const double Fascia = 0.2;
                PolySkirt(m, k, Sides, Rot, bodyR[t] + 0.12, bodyV1[t], eaveR[t] - 0.06, eaveV[t] - Fascia);
                if (lod == 0) PolyStruts(m, k, Sides, Rot, bodyR[t], bodyV1[t] - 0.1, eaveR[t], eaveV[t] - Fascia);
                PolyRoof(m, k, Sides, Rot, eaveR[t], eaveV[t], topR[t], topV[t], 0.1, Fascia, OctRoof, OctRib, OctFascia, lod);
            }
            // The gilt onion finial.
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, Fin - 0.02, 0), PashupatiKit.GiltBright,
                                 new[]
                                 {
                                     0.0, 0.0, 0.3, 0.0, 0.3, 0.08, 0.18, 0.14, 0.2, 0.2, 0.34, 0.38, 0.36, 0.48, 0.3, 0.62, 0.16, 0.76, 0.08, 0.86, 0.12, 0.9, 0.12, 0.96,
                                     0.05, 1.0, 0.03, 1.25, 0.0, OctagonTopM - Fin + 0.02,
                                 }, 12, lod, 0b0_0110_0000_0110);
            SacredDraw.Bake(m, v0, i0, f.Level);
            // Colliders: the walkable plinth, the body.
            f.Box(OctagonA - 3.3, OctagonA + 3.3, OctagonB - 3.3, OctagonB + 3.3, -0.25, 0.5, GenColliderFlags.Walkable, GenColliders.Stone);
            f.Cylinder(OctagonA, OctagonB, 0.5, 9.6, 2.35);
        }

        /// <summary>
        /// PT-14, the kings' pillars and the Dharmashila south of the temple (c_inner/c_01): two grey stone pillars on
        /// stepped bases with great lotus-bowl capitals, each carrying a gilt throne and a gilt royal figure kneeling with
        /// joined hands toward the temple under a rearing gilt naga hood; the flat oath stone and the old Chandeshvar linga
        /// on its yoni beside them.
        /// </summary>
        public static void KingsPillars(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            King(ref f, m, -6.5, -12.0, 2.5, lod);
            King(ref f, m, -3.4, -12.4, 2.1, lod);
            // The Dharmashila: a flat stone slab on two low feet.
            Affine3 d = f.At(-0.6, 0, -11.7);
            PashupatiKit.Block(m, d, -0.55, -0.35, 0, 0.16, -0.3, 0.3, PashupatiKit.StoneDark);
            PashupatiKit.Block(m, d, 0.35, 0.55, 0, 0.16, -0.3, 0.3, PashupatiKit.StoneDark);
            Shapes.RoundedBox(m, SacredDraw.At(d, 0, 0.24, 0), PashupatiKit.StonePlinth, 1.3, 0.16, 0.8, 0.03, lod == 0 ? 2 : 1);
            f.Box(-1.25, 0.05, -12.1, -11.3, 0, 0.32, GenColliderFlags.Walkable, GenColliders.Stone);
            // The Chandeshvar linga on its yoni, on a square stone base, the spout to the north.
            Affine3 c = f.Facing(2.0, 0, -11.2, 0, 1);
            if (lod >= 2)
            {
                PashupatiKit.Block(m, c, -0.5, 0.5, -0.05, 0.47, -0.5, 0.5, PashupatiKit.StoneDark);
                Shapes.Cylinder(m, SacredDraw.At(c, 0, 0.47, 0), PashupatiKit.StoneDark, 0.17, 0.6, 5, 0, 0, false, true);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            PashupatiKit.Band(m, c, 0, 0, 0.5, 0.5, new[] { 0.03, -0.05, 0.03, 0.1, 0.0, 0.14, 0.0, 0.3 }, PashupatiKit.StoneDark, true, PashupatiKit.StoneDark);
            PashupatiKit.LatheOf(m, SacredDraw.At(c, 0, 0.3, 0), PashupatiKit.StoneDark, new[] { 0.0, 0.0, 0.42, 0.0, 0.46, 0.08, 0.44, 0.16, 0.3, 0.17, 0.0, 0.17 }, 16, lod,
                                 0b11100);
            if (lod <= 1) SacredDraw.Box(m, c, -0.1, 0.1, 0.36, 0.45, 0.35, 0.72, PashupatiKit.StoneDark, BoxFaces.All & ~BoxFaces.Back & ~BoxFaces.Bottom);
            PashupatiKit.LatheOf(m, SacredDraw.At(c, 0, 0.45, 0), PashupatiKit.Br(0x3E3B38, MaterialChannel.Stone),
                                 new[] { 0.0, 0.0, 0.17, 0.0, 0.17, 0.45, 0.14, 0.56, 0.07, 0.62, 0.0, 0.63 }, 12, lod, 0b10);
            if (lod == 0)
            {
                // Vermilion and marigold offerings.
                Shapes.Sphere(m, SacredDraw.At(c, 0.0, 1.06, 0.06), PashupatiKit.Vermilion, 0.06, 5);
                for (int q = 0; q < 5; q++)
                {
                    double ang = 2 * Math.PI * q / 5;
                    Shapes.Sphere(m, SacredDraw.At(c, 0.3 * Math.Sin(ang), 0.5, 0.3 * Math.Cos(ang)), Marigold, 0.05, 4);
                }
            }
            f.Box(1.5, 2.5, -11.7, -10.7, 0, 1.1, GenColliderFlags.NoClimb, GenColliders.Stone);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        [ThreadStatic] private static Path3 _naga;

        /// <summary>Smoothing buffer for the naga's and the plume's curves.</summary>
        private static Path3 Smooth
        {
            get { return (_naga ?? (_naga = new Path3(64))).Clear(); }
        }

        /// <summary>One kings' pillar at site (a, b), its shaft <paramref name="shaft"/> m, the figure facing +b
        /// (c_inner/c_01): a two-step stone base, the dark octagonal-moulded shaft, the great grey lotus-bowl capital with
        /// two rows of petals, the gilt throne with a seated lion at each corner, the king (about 1.1 m) kneeling on his heels
        /// in a flared long coat with his forearms raised to the palms joined in namaste, his face under the plumed crown,
        /// and behind him the gilt naga (<see cref="Naga"/>): the tall body arching over him to its crowned head, and the
        /// spread hood of seven small heads curving over his crown.</summary>
        private static void King(ref PashupatiFrame f, MeshData m, double a, double b, double shaft, int lod)
        {
            Affine3 x = f.Facing(a, 0, b, 0, 1);
            ShapeBrush gold = PashupatiKit.GiltBright, shade = PashupatiKit.GiltRoof, deep = PashupatiKit.GiltEave;
            double c0 = 0.55 + shaft, t0 = c0 + 0.55, y0 = t0 + 0.44;
            if (lod >= 2)
            {
                // Far: the shaft, the capital bowl, the throne, the kneeling figure, the naga's rise and its hood.
                PashupatiKit.Block(m, x, -0.5, 0.5, -0.1, 0.55, -0.5, 0.5, PashupatiKit.StonePlinth);
                Shapes.Cylinder(m, SacredDraw.At(x, 0, 0.55, 0), PashupatiKit.StoneDark, 0.2, shaft, 5, 0, 0, false, false);
                Shapes.Frustum(m, SacredDraw.At(x, 0, c0, 0), PashupatiKit.StoneDark, 0.24, 0.6, 0.55, 5, 0, 0, false, true);
                PashupatiKit.Block(m, x, -0.4, 0.4, t0, y0, -0.4, 0.4, shade);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.45, 0.0), gold, 0.24, 0.5, 0.26, 4, false);
                Shapes.Bar(m, x, gold, 0, t0 + 0.3, -0.36, 0, y0 + 1.7, -0.3, 0.09, 4);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 1.78, -0.05), gold, 0.25, 0.06, 0.22, 4, false);
                return;
            }
            PashupatiKit.Block(m, x, -0.62, 0.62, -0.1, 0.3, -0.62, 0.62, PashupatiKit.StonePlinth);
            PashupatiKit.Block(m, x, -0.46, 0.46, 0.3, 0.55, -0.46, 0.46, PashupatiKit.StoneEdge);
            ShapeBrush stone = PashupatiKit.Br(0x77736C, MaterialChannel.Stone);
            PashupatiKit.LatheOf(m, SacredDraw.At(x, 0, 0.55, 0), stone,
                                 new[] { 0.0, 0.0, 0.3, 0.0, 0.3, 0.12, 0.24, 0.2, 0.19, 0.28, 0.18, shaft - 0.2, 0.24, shaft - 0.14, 0.24, shaft - 0.06, 0.2, shaft, 0.0, shaft },
                                 8, lod, 0b11_1110_0110);
            // The lotus-bowl capital: a narrow neck, the swelling bowl and its rim (c_inner/c_01: about as wide as the throne
            // and a half), two rows of petals.
            PashupatiKit.LatheOf(m, SacredDraw.At(x, 0, c0, 0), stone,
                                 new[] { 0.0, 0.0, 0.22, 0.0, 0.26, 0.05, 0.22, 0.1, 0.3, 0.14, 0.5, 0.24, 0.6, 0.36, 0.62, 0.46, 0.56, 0.53, 0.5, 0.55, 0.0, 0.55 }, 14, lod,
                                 0b10_0000_0110);
            if (lod == 0)
            {
                for (int row = 0; row < 2; row++)
                {
                    int n = row == 0 ? 8 : 10;
                    for (int q = 0; q < n; q++)
                    {
                        // Each petal lies on the bowl (tilted with its surface: out and up below the belly, in above it).
                        double ang = 2 * Math.PI * (q + 0.5 * row) / n, r = row == 0 ? 0.47 : 0.58;
                        Affine3 pf = Affine3.RotationX(row == 0 ? 0.9 : -0.5).Then(Affine3.RotationY(-ang))
                                            .Then(SacredDraw.At(x, r * Math.Sin(ang), c0 + (row == 0 ? 0.25 : 0.44), r * Math.Cos(ang)));
                        Shapes.Ellipsoid(m, pf, PashupatiKit.Ao(stone, row == 0 ? 0.85f : 0.95f), 0.13, 0.13, 0.035, 4, false);
                    }
                }
            }
            // The gilt throne: a moulded base, a carved body with a panel on each face, the cushion slab; a small seated lion
            // on the base at each corner, looking out along the diagonal.
            Shapes.RoundedBox(m, SacredDraw.At(x, 0, t0 + 0.06, 0), shade, 0.92, 0.12, 0.92, 0.03, lod == 0 ? 2 : 1);
            Shapes.RoundedBox(m, SacredDraw.At(x, 0, t0 + 0.26, 0), gold, 0.7, 0.28, 0.7, 0.03, lod == 0 ? 2 : 1);
            Shapes.RoundedBox(m, SacredDraw.At(x, 0, t0 + 0.42, 0), shade, 0.8, 0.06, 0.8, 0.02, 1);
            for (int c = 0; c < 4; c++)
            {
                double su = c % 2 == 0 ? 1 : -1, sw = c < 2 ? 1 : -1;
                Affine3 lf = Affine3.RotationY(Math.Atan2(su, sw)).Then(SacredDraw.At(x, su * 0.38, t0 + 0.12, sw * 0.38));
                ThroneLion(m, lf, gold, deep, lod);
            }
            if (lod == 0)
            {
                for (int sd = 0; sd < 3; sd++)
                {
                    Affine3 face = sd == 0 ? SacredDraw.At(x, 0, 0, 0.35) : SacredDraw.Basis(x, sd == 1 ? 0.35 : -0.35, 0, 0, 0, 0, sd == 1 ? -1 : 1, 0, 1, 0);
                    Shapes.Ellipsoid(m, SacredDraw.At(face, 0, t0 + 0.26, 0.0), deep, 0.17, 0.09, 0.025, 6, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(face, 0, t0 + 0.26, 0.02), gold, 0.06, 0.06, 0.02, 5, false);
                }
            }
            KneelingKing(m, x, y0, gold, shade, deep, lod);
            Naga(m, x, t0, y0, gold, deep, lod);
            f.Cylinder(a, b, 0, c0 + 0.55, 0.45);
        }

        [ThreadStatic] private static Profile2 _hoodSection;

        /// <summary>
        /// The gilt naga behind a king (c_inner/c_01): its body rises from the back of the throne straight up behind him,
        /// tapering, scaled down its front, and arches forward high over his head into the crowned serpent head looking
        /// forward and down; nearer, behind the king's back, the spread hood curves up past his crown and over it, widening
        /// into a fan about 0.5 m across with seven small heads along its front edge (side-on it is the curved blade of the
        /// photo, from the front the many-headed fan).
        /// </summary>
        private static void Naga(MeshData m, in Affine3 x, double t0, double y0, in ShapeBrush gold, in ShapeBrush deep, int lod)
        {
            int seg = lod == 0 ? 7 : 5;
            Path3 c1 = PashupatiKit.PathS;
            c1.Add(0, t0 + 0.28, -0.37).Add(0, y0 + 0.4, -0.43).Add(0, y0 + 1.0, -0.45).Add(0, y0 + 1.5, -0.41).Add(0, y0 + 1.84, -0.26)
              .Add(0, y0 + 1.98, -0.06).Add(0, y0 + 1.97, 0.08);
            Path3 body = Smooth;
            Curves.CatmullRom(c1, body, lod == 0 ? 4 : 2);
            Shapes.Tube(m, x, gold, body, 0.1, lod == 0 ? 10 : 6, true, false, default, 0.06);
            if (lod == 0)
            {
                // Belly scales: bands up the front of the rising body.
                for (int q = 0; q < 4; q++)
                {
                    double t = (q + 0.5) / 4, ry = 0.3 + 1.15 * t, r = 0.098 - 0.03 * t;
                    Affine3 band = Affine3.Scaling(1, 1, 0.8).Then(SacredDraw.At(x, 0, y0 + ry, -0.44 + 0.03 * t));
                    Shapes.Torus(m, band, deep, r + 0.004, 0.012, 8, 3);
                }
            }
            // The serpent head at the top of the arch: skull, snout, the jaw, eyes and a three-pointed crown.
            Affine3 hd = Affine3.RotationX(0.3).Then(SacredDraw.At(x, 0, y0 + 1.95, 0.15));
            Shapes.Ellipsoid(m, hd, gold, 0.08, 0.07, 0.12, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(hd, 0, -0.015, 0.12), gold, 0.055, 0.042, 0.075, seg, false);
            if (lod == 0)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(hd, 0, -0.05, 0.06), deep, 0.06, 0.02, 0.1, 6, false);
                for (int s = -1; s <= 1; s += 2) Shapes.Sphere(m, SacredDraw.At(hd, s * 0.055, 0.03, 0.05), deep, 0.018, 4);
                Shapes.Ellipsoid(m, Affine3.RotationX(-0.4).Then(SacredDraw.At(hd, 0, 0.02, -0.08)), gold, 0.11, 0.1, 0.025, 8, false);
            }
            for (int s = -1; s <= 1; s++)
                Shapes.Cone(m, SacredDraw.At(hd, 0.045 * s, 0.05, -0.01 - 0.02 * Math.Abs(s)), gold, 0.022, s == 0 ? 0.12 : 0.08, 4, 0, 0, false);
            // The spread hood behind the king: an upright sweep whose section widens from his shoulders to the fan over his
            // crown.
            Path3 c2 = PashupatiKit.PathS;
            c2.Add(0, y0 + 0.5, -0.25).Add(0, y0 + 0.95, -0.28).Add(0, y0 + 1.25, -0.21).Add(0, y0 + 1.39, -0.05).Add(0, y0 + 1.37, 0.1);
            Path3 hood = Smooth;
            Curves.CatmullRom(c2, hood, lod == 0 ? 4 : 2);
            Profile2 sec = _hoodSection ?? (_hoodSection = new Profile2(32));
            sec.SetEllipse(0.1, 0.032, lod == 0 ? 10 : 8);
            Shapes.Sweep(m, x, gold, hood, sec, false, true, SweepFrames.Upright, 1.0, 2.5);
            int heads = lod == 0 ? 7 : 5;
            for (int q = 0; q < heads; q++)
            {
                double ang = -1.15 + 2.3 * q / (heads - 1), hu = 0.21 * Math.Sin(ang);
                double hy = y0 + 1.37 + 0.035 * Math.Cos(ang), hz = 0.06 + 0.06 * Math.Cos(ang);
                Affine3 hf = Affine3.RotationX(0.45).Then(Affine3.RotationY(0.35 * ang)).Then(SacredDraw.At(x, hu, hy, hz));
                Shapes.Sphere(m, hf, gold, 0.046, lod == 0 ? 6 : 4);
                Shapes.Ellipsoid(m, SacredDraw.At(hf, 0, -0.005, 0.05), gold, 0.028, 0.022, 0.045, 4, false);
            }
        }

        /// <summary>The kneeling king on the throne slab at height <paramref name="y0"/>, facing +z (c_inner/c_01): sitting
        /// on his heels, the long coat flared round the folded legs, the torso and broad shoulders, the arms bent with the
        /// forearms raised to the joined palms before the chest, the face (brow, nose, moustache, ears) under the round crown
        /// with its brim, jewel and the bird-of-paradise plume sweeping back.</summary>
        private static void KneelingKing(MeshData m, in Affine3 x, double y0, in ShapeBrush gold, in ShapeBrush shade, in ShapeBrush deep, int lod)
        {
            int seg = lod == 0 ? 8 : 6;
            // The folded legs and the coat's skirt flaring over them.
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.12, 0.03), gold, 0.27, 0.13, 0.29, seg, false);
            Shapes.Frustum(m, SacredDraw.At(x, 0, y0 + 0.06, -0.02), shade, 0.29, 0.17, 0.4, seg, 0, 0, false, false);
            if (lod == 0) Shapes.Torus(m, SacredDraw.At(x, 0, y0 + 0.08, -0.02), deep, 0.285, 0.018, 16, 3);
            // Torso, waist sash, shoulders.
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.56, -0.02), gold, 0.17, 0.21, 0.12, seg, false);
            if (lod == 0) Shapes.Torus(m, SacredDraw.At(x, 0, y0 + 0.42, -0.02), deep, 0.165, 0.02, 14, 3);
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.71, -0.03), gold, 0.22, 0.075, 0.12, seg, false);
            // Neck and head.
            Shapes.Cylinder(m, SacredDraw.At(x, 0, y0 + 0.74, -0.02), gold, 0.055, 0.1, lod == 0 ? 8 : 5, 0, 0, false, false);
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.9, 0.0), gold, 0.092, 0.115, 0.1, seg, false);
            if (lod == 0)
            {
                // The face: brow ridge, nose, moustache, ears.
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.93, 0.085), shade, 0.07, 0.016, 0.02, 6, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.885, 0.1), gold, 0.018, 0.034, 0.024, 5, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.852, 0.094), deep, 0.04, 0.01, 0.014, 5, false);
                for (int s = -1; s <= 1; s += 2) Shapes.Ellipsoid(m, SacredDraw.At(x, s * 0.092, y0 + 0.9, -0.005), gold, 0.016, 0.034, 0.024, 4, false);
            }
            // The crown: a round cap on a brim, the jewel at its front, a knob on top and the plume sweeping back.
            Shapes.Frustum(m, SacredDraw.At(x, 0, y0 + 0.96, -0.01), shade, 0.112, 0.096, 0.12, seg, 0.03, 2, false, true);
            Shapes.Torus(m, SacredDraw.At(x, 0, y0 + 0.97, -0.01), gold, 0.112, 0.016, lod == 0 ? 14 : 8, 3);
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 1.02, 0.1), gold, 0.03, 0.04, 0.02, 5, false);
            Shapes.Sphere(m, SacredDraw.At(x, 0, y0 + 1.1, -0.01), gold, 0.03, 5);
            Path3 pc = PashupatiKit.PathS;
            pc.Add(0, y0 + 1.1, -0.02).Add(0, y0 + 1.19, -0.05).Add(0, y0 + 1.24, -0.12).Add(0, y0 + 1.22, -0.19);
            Path3 plume = Smooth;
            Curves.CatmullRom(pc, plume, lod == 0 ? 3 : 1);
            Shapes.Tube(m, x, gold, plume, 0.034, lod == 0 ? 6 : 4, true, false, default, 0.01);
            // The arms: upper arms down and slightly forward to the elbows, the forearms up to the joined palms.
            for (int s = -1; s <= 1; s += 2)
            {
                Shapes.Bar(m, x, gold, s * 0.19, y0 + 0.69, -0.03, s * 0.21, y0 + 0.5, 0.05, 0.05, lod == 0 ? 7 : 4);
                Shapes.Sphere(m, SacredDraw.At(x, s * 0.21, y0 + 0.5, 0.05), gold, 0.05, lod == 0 ? 6 : 4);
                Shapes.Bar(m, x, gold, s * 0.21, y0 + 0.5, 0.05, s * 0.035, y0 + 0.63, 0.17, 0.042, lod == 0 ? 7 : 4);
            }
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, y0 + 0.7, 0.18), gold, 0.042, 0.095, 0.05, lod == 0 ? 7 : 5, false);
        }

        /// <summary>A small gilt lion (about 0.24 m) seated on its haunches at the throne's corner, facing +z: the haunch,
        /// the upright chest, two straight forelegs, the maned head and its muzzle.</summary>
        private static void ThroneLion(MeshData m, in Affine3 k, in ShapeBrush gold, in ShapeBrush deep, int lod)
        {
            if (lod >= 1)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.09, -0.01), gold, 0.06, 0.09, 0.07, 4, false);
                Shapes.Sphere(m, SacredDraw.At(k, 0, 0.19, 0.03), gold, 0.05, 5);
                return;
            }
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.05, -0.03), gold, 0.065, 0.05, 0.075, 4, false);
            Shapes.Ellipsoid(m, Affine3.RotationX(-0.35).Then(SacredDraw.At(k, 0, 0.11, 0.0)), gold, 0.05, 0.075, 0.055, 4, false);
            for (int s = -1; s <= 1; s += 2) Shapes.Bar(m, k, gold, s * 0.03, 0.1, 0.035, s * 0.032, 0.0, 0.05, 0.016, 4);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.19, 0.035), deep, 0.065, 0.062, 0.045, 4, false);
            Shapes.Sphere(m, SacredDraw.At(k, 0, 0.2, 0.05), gold, 0.044, 4);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.185, 0.09), gold, 0.026, 0.022, 0.024, 4, false);
        }

        /// <summary>
        /// The shrines between the main temple and the South Gate range: the maroon fluted-dome shrine on its dark stone
        /// body with four small corner domes and its smaller fluted bell-dome neighbour (pagoda/ref_02, c_gje/c_03), and
        /// behind the Nandi the row of grey stone linga shrines with ribbed domes before the red wall and its black iron
        /// railing of small tridents (c_inner/c_00).
        /// </summary>
        public static void SouthShrines(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            MaroonShrine(ref f, m, -17.6, -12.4, lod);
            MaroonBell(ref f, m, -14.3, -12.7, lod);
            for (int q = 0; q < 3; q++)
            {
                double b = -12.6 + 2.2 * q;
                StoneShrine(m, f.Facing(-21.9, 0, b, 1, 0), 1.0, lod);
                f.Box(-22.35, -21.45, b - 0.45, b + 0.45, 0, 1.9, GenColliderFlags.NoClimb, GenColliders.Stone);
            }
            RailingWall(ref f, m, lod);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        private static void MaroonShrine(ref PashupatiFrame f, MeshData m, double a, double b, int lod)
        {
            Affine3 k = f.At(a, 0, b);
            if (lod >= 2)
            {
                PashupatiKit.Block(m, k, -0.85, 0.85, -0.1, 1.95, -0.85, 0.85, PashupatiKit.StoneWall);
                Shapes.Cone(m, SacredDraw.At(k, 0, 1.95, 0), Maroon, 1.0, 1.4, 6, 0, 0, true);
                Shapes.Cone(m, SacredDraw.At(k, 0, 3.2, 0), PashupatiKit.GiltBright, 0.15, 0.7, 4, 0, 0, false);
                return;
            }
            PashupatiKit.Band(m, k, 0, 0, 1.05, 1.05, new[] { 0.0, -0.1, 0.0, 0.3 }, PashupatiKit.StonePlinth, true, Flag);
            PashupatiKit.Band(m, k, 0, 0, 0.82, 0.82, new[] { 0.0, 0.3, 0.0, 1.7, 0.1, 1.76, 0.1, 1.95 }, PashupatiKit.StoneWall, true, PashupatiKit.StoneWall);
            if (lod <= 1)
                for (int s = 0; s < 4; s++)
                    SacredParts.LatticeWindow(m, Face(k, s, 0.82, 0.82), 0, 1.05, 0, 0.62, 0.8, PashupatiKit.WoodDark, lod, 3);
            Fluted(m, SacredDraw.At(k, 0, 1.95, 0), Maroon, new[] { 0.95, 0.0, 0.98, 0.1, 0.93, 0.35, 0.8, 0.65, 0.58, 0.9, 0.32, 1.04, 0.12, 1.1, 0.0, 1.12 }, 16,
                   0.1, lod);
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, 1.93, 0), PashupatiKit.GiltRoof, new[] { 0.94, 0.0, 1.01, 0.03, 1.01, 0.13, 0.95, 0.16 }, 16, lod, 0b0110);
            if (lod <= 1)
                for (int c = 0; c < 4; c++)
                {
                    double cu = (c % 2 == 0 ? 1 : -1) * 0.78, cw = (c < 2 ? 1 : -1) * 0.78;
                    Fluted(m, SacredDraw.At(k, cu, 1.95, cw), Maroon, new[] { 0.24, 0.0, 0.25, 0.08, 0.2, 0.25, 0.1, 0.36, 0.0, 0.38 }, 8, 0.12, lod + 1);
                    PashupatiKit.BellFinial(m, k, cu, 2.32, cw, 0.2, 0.05, PashupatiKit.GiltBright, lod + 1);
                }
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, 3.05, 0), MaroonDark, new[] { 0.0, 0.0, 0.2, 0.0, 0.16, 0.12, 0.22, 0.2, 0.0, 0.22 }, 10, lod);
            PashupatiKit.BellFinial(m, k, 0, 3.26, 0, 0.62, 0.17, PashupatiKit.GiltBright, lod);
            f.Box(a - 1.05, a + 1.05, b - 1.05, b + 1.05, -0.1, 0.3, GenColliderFlags.Walkable, GenColliders.Stone);
            f.Box(a - 0.9, a + 0.9, b - 0.9, b + 0.9, 0.3, 3.0, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        private static void MaroonBell(ref PashupatiFrame f, MeshData m, double a, double b, int lod)
        {
            Affine3 k = f.At(a, 0, b);
            if (lod >= 2)
            {
                PashupatiKit.Block(m, k, -0.75, 0.75, -0.1, 1.05, -0.75, 0.75, RedPlasterDark);
                Shapes.Cone(m, SacredDraw.At(k, 0, 1.05, 0), Maroon, 0.88, 1.5, 6, 0, 0, true);
                return;
            }
            PashupatiKit.Band(m, k, 0, 0, 0.85, 0.85, new[] { 0.0, -0.1, 0.0, 0.25 }, PashupatiKit.StonePlinth, true, Flag);
            PashupatiKit.Band(m, k, 0, 0, 0.72, 0.72, new[] { 0.04, 0.25, 0.04, 0.35, 0.0, 0.4, 0.0, 0.92, 0.08, 0.98, 0.08, 1.05 }, RedPlasterDark, true, MaroonDark);
            if (lod <= 1) SacredDraw.Panel(m, Face(k, 2, 0.72, 0.72), -0.22, 0.42, 0.22, 0.86, 0.005, PashupatiKit.Ao(PashupatiKit.WoodDark, 0.6f));
            Fluted(m, SacredDraw.At(k, 0, 1.05, 0), Maroon,
                   new[] { 0.86, 0.0, 0.9, 0.05, 0.8, 0.12, 0.74, 0.3, 0.65, 0.58, 0.52, 0.82, 0.32, 0.98, 0.12, 1.05, 0.0, 1.07 }, 18, 0.09, lod);
            PashupatiKit.BellFinial(m, k, 0, 2.08, 0, 0.5, 0.12, PashupatiKit.GiltBright, lod);
            f.Box(a - 0.85, a + 0.85, b - 0.85, b + 0.85, -0.1, 2.2, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        /// <summary>A grey stone linga shrine (c_inner/c_00): a square stone shaft with a niche, a cornice and a ribbed
        /// (fluted) stone dome with a knob, about 2.2 m; the niche on the local +z face.</summary>
        private static void StoneShrine(MeshData m, in Affine3 x, double s, int lod)
        {
            if (lod >= 2)
            {
                PashupatiKit.Block(m, x, -0.34 * s, 0.34 * s, -0.1, 1.4 * s, -0.34 * s, 0.34 * s, PashupatiKit.StoneDark);
                Shapes.Cone(m, SacredDraw.At(x, 0, 1.4 * s, 0), PashupatiKit.StonePlinth, 0.38 * s, 0.75 * s, 5, 0, 0, false);
                return;
            }
            PashupatiKit.Band(m, x, 0, 0, 0.42 * s, 0.42 * s, new[] { 0.03, -0.1, 0.03, 0.12, 0.0, 0.16, 0.0, 0.2 }, PashupatiKit.StoneEdge, true, PashupatiKit.StoneEdge);
            PashupatiKit.Band(m, x, 0, 0, 0.32 * s, 0.32 * s, new[] { 0.0, 0.2, 0.0, 1.22 * s, 0.06, 1.27 * s, 0.06, 1.4 * s }, PashupatiKit.StoneDark, true,
                              PashupatiKit.StoneDark);
            if (lod <= 1) SacredDraw.Panel(m, x, -0.12 * s, 0.5 * s, 0.12 * s, 0.92 * s, 0.32 * s + 0.005, PashupatiKit.Ao(PashupatiKit.StoneDark, 0.45f));
            Fluted(m, SacredDraw.At(x, 0, 1.4 * s, 0), PashupatiKit.StonePlinth,
                   new[] { 0.36 * s, 0.0, 0.38 * s, 0.1 * s, 0.35 * s, 0.26 * s, 0.24 * s, 0.4 * s, 0.1 * s, 0.48 * s, 0.0, 0.5 * s }, 12, 0.14, lod);
            PashupatiKit.LatheOf(m, SacredDraw.At(x, 0, 1.88 * s, 0), PashupatiKit.StoneEdge,
                                 new[] { 0.0, 0.0, 0.08 * s, 0.0, 0.1 * s, 0.08 * s, 0.05 * s, 0.14 * s, 0.07 * s, 0.2 * s, 0.0, 0.3 * s }, 8, lod);
        }

        /// <summary>The red wall with its black iron railing of small tridents on the courtyard's west side behind the
        /// Nandi (c_inner/c_00), along the face of the building w913170313.</summary>
        private static void RailingWall(ref PashupatiFrame f, MeshData m, int lod)
        {
            const double A = -23.0, B0 = -13.8, B1 = -6.6, H = 2.5, Rail = 1.95;
            double half = 0.5 * (B1 - B0);
            // z toward the Nandi (+a), x along −b.
            Affine3 x = f.Facing(A, 0, 0.5 * (B0 + B1), 1, 0);
            SacredDraw.Box(m, x, -half, half, 0, H, -0.22, -0.1, WallRed, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, x, -half, half, 0, 0.4, -0.1, 0.14, PashupatiKit.StoneDark, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            f.Wall(A, B0, A, B1, 0, H, 0.2);
            if (lod >= 2) return;
            // Posts, top and bottom rails.
            int posts = (int)Math.Round(2 * half / 1.1);
            for (int q = 0; q <= posts; q++)
            {
                double u = -half + 2 * half * q / posts;
                SacredDraw.Box(m, x, u - 0.035, u + 0.035, 0.4, Rail, 0.0, 0.07, Iron, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
            }
            SacredDraw.Box(m, x, -half, half, Rail - 0.05, Rail, 0.0, 0.06, Iron, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            SacredDraw.Box(m, x, -half, half, 0.5, 0.55, 0.0, 0.06, Iron, BoxFaces.Front | BoxFaces.Top);
            if (lod > 0) return;
            // Bars, and a small trident every 0.55 m along the top rail.
            int bars = (int)Math.Round(2 * half / 0.14);
            for (int q = 1; q < bars; q++)
            {
                double u = -half + 2 * half * q / bars;
                SacredDraw.Box(m, x, u - 0.012, u + 0.012, 0.55, Rail, 0.02, 0.04, Iron, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            int tri = (int)Math.Round(2 * half / 0.55);
            for (int q = 0; q <= tri; q++)
            {
                double u = -half + 2 * half * q / tri;
                Shapes.Bar(m, x, Iron, u, Rail, 0.03, u, Rail + 0.42, 0.03, 0.014, 3);
                Path3 p = PashupatiKit.PathS;
                p.Add(u - 0.12, Rail + 0.36, 0.03).Add(u - 0.11, Rail + 0.24, 0.03).Add(u, Rail + 0.2, 0.03).Add(u + 0.11, Rail + 0.24, 0.03).Add(u + 0.12, Rail + 0.36, 0.03);
                Shapes.Tube(m, x, Iron, p, 0.012, 3, false);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // North court
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>PT-06, the Vasuki (Basukinath) temple (c_inner/c_07, c_inner/c_21): a small two-tier pagoda on a low
        /// plinth with verdigris copper roofs, curled tips, carved struts and lattice skirts, a carved wooden upper body
        /// and a gilt bell finial to ≈ 8.5 m; door to the west.</summary>
        public static void Vasuki(ref PashupatiFrame f, MeshData m)
        {
            var p = new TwoTierSpec
            {
                // Squat and spreading (c_inner/c_07, c_inner/c_09): a broad lower roof at about 21°, a short dark upper storey
                // with its window band, the upper eave about 0.64 of the lower, both roofs at about 21°, upturned gilt corners.
                PlinthU = 1.95, PlinthW = 1.95, PlinthH = 0.45, WallU = 1.42, WallW = 1.42, WallTop = 3.1, CorniceTop = 3.35, Eave1U = 2.7, Eave1W = 2.7,
                Eave1V = 4.25, Top1V = 4.94, BodyU = 0.82, BodyW = 0.82, BodyTop = 5.5, Eave2U = 1.73, Eave2W = 1.73, Eave2V = 6.06, TopV = 6.6, TopHalf = 0.32,
                FinialH = 1.05, FinialR = 0.32, Lift = 0.2, Curl = 0.4, SeamPitch = 0.65, Struts1 = 3, Struts2 = 4, DoorSide = 1, Roof = PashupatiKit.Verdigris,
                Rib = PashupatiKit.VerdigrisRib, Fascia = VerdigrisFascia, CurlB = PashupatiKit.GiltBright, Wall = GlazedWall, Body = PashupatiKit.WoodDark,
                Seed = 601u,
            };
            int v0 = m.VertexCount, i0 = m.IndexCount;
            TwoTier(ref f, m, VasukiA, VasukiB, VasukiYaw, p);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>PT-10, the other shrines of the north-east group on their OSM squares: white plastered
        /// mini-shikharas with curved towers, amalakas and gilt bell finials, and the white shrine with a lotus dome and
        /// four gilt bells on its flat roof (c_inner/c_01); doors toward the court.</summary>
        public static void SmallShrines(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            for (int q = 0; q < Squares.Length / 3; q++)
            {
                double a = Squares[3 * q], b = Squares[3 * q + 1], yaw = Squares[3 * q + 2];
                if (q == 1) BellShrine(ref f, m, a, b, yaw, 1.55, lod);
                else WhiteShikhara(ref f, m, a, b, yaw, 1.45, SquareHeights[q], 2, lod);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>A white plastered mini-shikhara at site (a, b) turned by yaw: a plinth, the square cella with its door
        /// on side <paramref name="doorSide"/> and blind niches, the cornice, the curved cross-planned tower in
        /// horizontal courses, the ribbed amalaka and the gilt bell finial to <paramref name="height"/>.</summary>
        private static void WhiteShikhara(ref PashupatiFrame f, MeshData m, double a, double b, double yaw, double half, double height, int doorSide, int lod)
        {
            Scratch();
            Affine3 k = Yawed(ref f, a, 0, b, yaw);
            const double PlinthH = 0.35, TopScale = 0.42;
            double body = 0.72 * half, bodyTop = PlinthH + 1.55, cornice = bodyTop + 0.18, towerTop = height - 0.75;
            ShapeBrush white = PashupatiKit.White;
            PashupatiKit.Band(m, k, 0, 0, half + 0.12, half + 0.12,
                              lod >= 2 ? new[] { 0.0, -0.2, 0.0, PlinthH } : new[] { 0.04, -0.2, 0.04, 0.06, 0.0, 0.1, 0.0, PlinthH - 0.06, 0.04, PlinthH - 0.03, 0.04, PlinthH },
                              PashupatiKit.StonePlinth, true, Flag);
            PashupatiKit.Band(m, k, 0, 0, body, body, new[] { 0.06, PlinthH, 0.06, PlinthH + 0.15, 0.0, PlinthH + 0.2, 0.0, bodyTop, 0.1, bodyTop + 0.06, 0.1, cornice },
                              white, true, white);
            for (int s = 0; s < 4; s++)
            {
                Affine3 x = Face(k, s, body, body);
                if (s == doorSide)
                    SacredParts.Door(m, x, 0, PlinthH + 0.2, 0, 0.62, 1.05, PashupatiKit.WoodDark, lod, true, bodyTop - 0.02);
                else if (lod <= 1)
                    SacredDraw.Panel(m, x, -0.28, PlinthH + 0.45, 0.28, bodyTop - 0.3, 0.004, WhiteShade);
            }
            // The tower: a cross plan (a central projection on each face) shrinking in a gentle convex curve.
            double h0 = 0.95 * body;
            int n = Cross(h0, 0.32 * h0, 0.12 * h0, _pu, _pw);
            int stations = lod == 0 ? 7 : lod == 1 ? 4 : 2;
            Tower(m, k, _pu, _pw, n, cornice, towerTop, stations, TopScale, white, WhiteShade);
            for (int i = 0; i < n; i++)
            {
                _su[i] = _pu[i] * TopScale;
                _sw[i] = _pw[i] * TopScale;
            }
            SacredDraw.Cap(m, k, _su, _sw, n, 0, towerTop, true, white);
            // Amalaka and finial.
            double r = 1.2 * TopScale * h0;
            Fluted(m, SacredDraw.At(k, 0, towerTop - 0.02, 0), white, new[] { 0.0, 0.0, 0.75 * r, 0.0, r, 0.08, r, 0.16, 0.75 * r, 0.24, 0.0, 0.25 }, 16, 0.14, lod);
            PashupatiKit.BellFinial(m, k, 0, towerTop + 0.22, 0, height - towerTop - 0.22, 0.16, PashupatiKit.GiltBright, lod);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            f.BoxAlong(a, b, half + 0.12, half + 0.12, cy, sy, -0.2, PlinthH, GenColliderFlags.Walkable, GenColliders.Stone);
            f.BoxAlong(a, b, body, body, cy, sy, PlinthH, height - 1.2, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        /// <summary>The white shrine with a lotus dome and four gilt bells on its flat roof (c_inner/c_01).</summary>
        private static void BellShrine(ref PashupatiFrame f, MeshData m, double a, double b, double yaw, double half, int lod)
        {
            Affine3 k = Yawed(ref f, a, 0, b, yaw);
            const double PlinthH = 0.35, Top = 2.35, Roof = 2.55;
            ShapeBrush white = PashupatiKit.White;
            PashupatiKit.Band(m, k, 0, 0, half + 0.1, half + 0.1, new[] { 0.0, -0.2, 0.0, PlinthH }, PashupatiKit.StonePlinth, true, Flag);
            PashupatiKit.Band(m, k, 0, 0, half, half, new[] { 0.05, PlinthH, 0.05, PlinthH + 0.12, 0.0, PlinthH + 0.16, 0.0, Top, 0.12, Top + 0.06, 0.12, Roof }, white, true,
                              PashupatiKit.Ao(white, 0.9f));
            for (int s = 0; s < 4; s++)
            {
                Affine3 x = Face(k, s, half, half);
                if (s == 2) SacredParts.Door(m, x, 0, PlinthH + 0.16, 0, 0.8, 1.45, PashupatiKit.WoodDark, lod, true, Top - 0.02);
                else if (lod <= 1) SacredDraw.Panel(m, x, -0.35, PlinthH + 0.5, 0.35, Top - 0.35, 0.004, WhiteShade);
            }
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, Roof, 0), white, new[] { 0.0, 0.0, 0.78, 0.0, 0.8, 0.1, 0.72, 0.38, 0.52, 0.68, 0.22, 0.86, 0.0, 0.9 }, 16, lod, 0b10);
            PashupatiKit.BellFinial(m, k, 0, Roof + 0.86, 0, 0.62, 0.15, PashupatiKit.GiltBright, lod);
            double c = half - 0.22;
            for (int q = 0; q < 4; q++)
                PashupatiKit.BellFinial(m, k, (q % 2 == 0 ? 1 : -1) * c, Roof, (q < 2 ? 1 : -1) * c, 0.56, 0.21, PashupatiKit.GiltBright, lod);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            f.BoxAlong(a, b, half + 0.1, half + 0.1, cy, sy, -0.2, Roof, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        /// <summary>
        /// PT-10 (3), the red-plastered pyramidal shikhara with its gilt crown (c_inner/c_01; one stands between the Mukti
        /// Mandap and the East Gate, c_gje/c_03, c_04) at site (a, b), <paramref name="height"/> tall, its door to the
        /// courtyard (west). Drawn into the caller's bake range (no AO bake of its own).
        /// </summary>
        public static void RedShikhara(ref PashupatiFrame f, MeshData m, double a, double b, double height)
        {
            int lod = f.Lod;
            if (lod >= 3) return;
            Affine3 k = f.At(a, 0, b);
            const double Half = 0.95, PlinthH = 0.3, Body = 1.45, Cornice = 1.6;
            double crown = height - 1.0;
            PashupatiKit.Band(m, k, 0, 0, Half + 0.18, Half + 0.18, new[] { 0.0, -0.2, 0.0, PlinthH }, PashupatiKit.StoneDark, true, Flag);
            PashupatiKit.Band(m, k, 0, 0, Half, Half, new[] { 0.04, PlinthH, 0.04, PlinthH + 0.1, 0.0, PlinthH + 0.14, 0.0, Body, 0.08, Body + 0.05, 0.08, Cornice },
                              RedPlaster, true, RedPlasterDark);
            SacredParts.Door(m, Face(k, 1, Half, Half), 0, PlinthH + 0.14, 0, 0.55, 1.05, RedPlasterDark, lod, false);
            // The pyramid.
            double r0 = Half + 0.04, r1 = 0.22, rise = crown - Cornice, run = r0 - r1, l = Math.Sqrt(rise * rise + run * run);
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, cu, cw, du, dw, ou, ow;
                PashupatiKit.OnRect(s, -1, r0, r0, out au, out aw);
                PashupatiKit.OnRect(s, 1, r0, r0, out bu, out bw);
                PashupatiKit.OnRect(s, 1, r1, r1, out cu, out cw);
                PashupatiKit.OnRect(s, -1, r1, r1, out du, out dw);
                PashupatiKit.Out(s, out ou, out ow);
                SacredDraw.Quad(m, k, au, Cornice, aw, bu, Cornice, bw, cu, crown, cw, du, crown, dw, ou * rise / l, run / l, ow * rise / l, RedPlaster);
            }
            // The gilt crown: a collar with drooping petals, the red bulb, a gilt lotus and the gilt finial.
            PashupatiKit.Band(m, k, 0, 0, r1, r1, new[] { 0.06, crown - 0.32, 0.06, crown - 0.05, 0.0, crown }, PashupatiKit.GiltRoof, true, PashupatiKit.GiltRoof);
            if (lod <= 1)
            {
                int petals = lod == 0 ? 5 : 3;
                for (int s = 0; s < 4; s++)
                {
                    double ou, ow, su, sw;
                    PashupatiKit.Out(s, out ou, out ow);
                    PashupatiKit.Along(s, out su, out sw);
                    for (int q = 0; q < petals; q++)
                    {
                        double t = -0.8 + 1.6 * (q + 0.5) / petals;
                        double py = crown - 0.24 - 0.06 * (1 - Math.Abs(t)), hw = r0 - run * (py - Cornice) / rise, off = hw + 0.03;
                        double pu = su * t * hw + ou * off, pw = sw * t * hw + ow * off;
                        Affine3 pf = Affine3.RotationX(-0.4).Then(SacredDraw.Basis(k, pu, py, pw, su, 0, sw, 0, 1, 0));
                        Shapes.Ellipsoid(m, pf, PashupatiKit.GiltBright, 0.07, 0.17, 0.03, 5, false);
                    }
                }
            }
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, crown, 0), RedPlaster, new[] { 0.0, 0.0, 0.24, 0.0, 0.3, 0.12, 0.26, 0.28, 0.12, 0.36, 0.0, 0.37 }, 10, lod);
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, crown + 0.34, 0), PashupatiKit.GiltBright, new[] { 0.0, 0.0, 0.2, 0.0, 0.24, 0.06, 0.16, 0.12, 0.0, 0.13 }, 10, lod,
                                 0b10);
            PashupatiKit.BellFinial(m, k, 0, crown + 0.45, 0, height - crown - 0.45, 0.13, PashupatiKit.GiltBright, lod);
            f.Box(a - Half - 0.18, a + Half + 0.18, b - Half - 0.18, b + Half + 0.18, -0.2, PlinthH, GenColliderFlags.Walkable, GenColliders.Stone);
            f.Box(a - Half, a + Half, b - Half, b + Half, PlinthH, crown, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        /// <summary>
        /// The site's skyline trees (pashupati-temple.md 2.3): the two tall Araucarias north of the courtyard, right of the
        /// temple in every east-bank view (c_gje/c_16, complex/ref_09), and the tall thin columnar Araucaria with its tufted
        /// umbrella crown in the western area beyond the forecourt (c_westgate/c_29), which rises behind and just right of the
        /// gajur in the classic east-bank photos (roof/ref_07, complex/ref_09).
        /// </summary>
        public static void Araucarias(ref PashupatiFrame f, MeshData m)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Araucaria(ref f, m, 2.0, 44.0, 28.5, 11u);
            Araucaria(ref f, m, 14.8, 39.5, 30.0, 23u);
            Columnar(ref f, m, ColumnarA, ColumnarB, 36.0, 37u);
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>Site position of the columnar tree: west of the forecourt (which ends at a −78) on the classic view's
        /// sight line just right of the gajur [E from roof/ref_07, complex/ref_09, c_westgate/c_29].</summary>
        public const double ColumnarA = -84.0, ColumnarB = 4.0;

        /// <summary>
        /// An Araucaria h m tall at site (a, b) (c_inner/c_07, c_gje/c_12, c_gje/c_16): the straight trunk, bare low down;
        /// a dense pillar of short branch tufts hugging it (about 3 m across) up to about seven tenths of its height, made
        /// of irregular clumps turned and jittered level by level; and on top the broad, rounded head (about 7 m across)
        /// of a ring of drooping clumps under a flattish crown clump, the tree's mushroom silhouette.
        /// </summary>
        private static void Araucaria(ref PashupatiFrame f, MeshData m, double a, double b, double h, uint seed)
        {
            int lod = f.Lod;
            double g = f.GroundAt(a, b, 0);
            double top = Math.Min(AraucariaTopM, g + h), foot = g - 1.0, H = top - foot;
            Affine3 x = f.At(a, foot, b);
            Shapes.Frustum(m, x, Bark, 0.48, 0.14, H - 0.6, PashupatiKit.Seg(8, lod), 0, 0, false, false);
            double p0 = 0.24 * H, p1 = 0.7 * H, dome = H - p1, dr = 3.6 + 0.6 * (0.5 + 0.5 * ShapeNoise.Unit(1, 1, 1, seed));
            if (lod >= 2)
            {
                // Far: the pillar and the head.
                Shapes.Cylinder(m, SacredDraw.At(x, 0, p0, 0), Needles, 1.45, p1 - p0 + 0.4, lod >= 3 ? 5 : 6, 0, 0, lod < 3, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, p1 + 0.5 * dome, 0), NeedlesLit, dr, 0.55 * dome, dr, lod >= 3 ? 4 : 5, false);
                return;
            }
            int seg = lod == 0 ? 5 : 4;
            // The pillar: levels of 3 clumps (LOD1 2), each turned and jittered, slightly wider toward the head.
            int levels = lod == 0 ? 8 : 6, per = lod == 0 ? 3 : 2;
            double lh = (p1 - p0) / levels;
            for (int l = 0; l < levels; l++)
            {
                double t = (l + 0.5) / levels, y = p0 + lh * (l + 0.5) + 0.25 * lh * ShapeNoise.Unit(l, 1, 5, seed);
                double spin = 2 * Math.PI * ShapeNoise.Unit(l, 2, 5, seed);
                for (int k = 0; k < per; k++)
                {
                    double ang = spin + 2 * Math.PI * k / per, r = 0.9 + 0.2 * t + 0.3 * (0.5 + 0.5 * ShapeNoise.Unit(l, k, 6, seed));
                    ShapeBrush br = (l + k) % 3 == 0 ? NeedlesLit : Needles;
                    Shapes.Ellipsoid(m, SacredDraw.Turn(x, 0.5 * r * Math.Cos(ang), y, 0.5 * r * Math.Sin(ang), ang), br, 0.8 * r, 0.75 * lh + 0.35, 0.7 * r, lod == 0 ? 2 : seg,
                                     lod == 0);
                }
            }
            // The head: a ring of clumps hanging out over the pillar, their tips drooping, under a flattish crown clump.
            int ring = lod == 0 ? 7 : 5;
            double rs = 2 * Math.PI * ShapeNoise.Unit(3, 3, 3, seed);
            for (int k = 0; k < ring; k++)
            {
                double ang = rs + 2 * Math.PI * (k + 0.25 * ShapeNoise.Unit(k, 4, 7, seed)) / ring, rr = dr * (0.58 + 0.1 * ShapeNoise.Unit(k, 5, 7, seed));
                double y = p1 + 0.32 * dome + 0.4 * ShapeNoise.Unit(k, 6, 7, seed);
                ShapeBrush br = k % 2 == 0 ? NeedlesLit : Needles;
                Shapes.Ellipsoid(m, SacredDraw.Turn(x, rr * Math.Cos(ang), y, rr * Math.Sin(ang), ang), br, 0.5 * dr, 0.36 * dome, 0.42 * dr, lod == 0 ? 2 : seg, lod == 0);
                if (lod == 0)
                    Shapes.Ellipsoid(m, SacredDraw.Turn(x, 1.25 * rr * Math.Cos(ang), y - 0.3 * dome, 1.25 * rr * Math.Sin(ang), ang), Needles, 0.25 * dr, 0.22 * dome,
                                     0.22 * dr, 1, true);
            }
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, p1 + 0.62 * dome, 0), NeedlesLit, 0.68 * dr, 0.38 * dome, 0.68 * dr, lod == 0 ? 3 : 5, lod == 0);
            f.Cylinder(a, b, g, g + 4, 0.45);
        }

        /// <summary>
        /// The tall thin columnar Araucaria west of the temple (c_westgate/c_29): a straight bare trunk with a few small
        /// branch tufts up its upper half, then from about two thirds of its height a dense, flat-topped umbrella crown of
        /// about eight irregular clumps.
        /// </summary>
        private static void Columnar(ref PashupatiFrame f, MeshData m, double a, double b, double h, uint seed)
        {
            int lod = f.Lod;
            double g = f.GroundAt(a, b, PashupatiSite.ForecourtRiseM);
            double top = Math.Min(AraucariaTopM, g + h), foot = g - 1.0, H = top - foot, c0 = H - 8.5;
            Affine3 x = f.At(a, foot, b);
            Shapes.Frustum(m, x, Bark, 0.36, 0.12, H - 1.2, PashupatiKit.Seg(8, lod), 0, 0, false, false);
            if (lod >= 3)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(x, 0, H - 3.4, 0), Needles, 2.6, 2.8, 2.6, 4, false);
                return;
            }
            int seg = lod == 0 ? 6 : 4;
            if (lod <= 1)
            {
                // Small tufts up the upper trunk.
                int n = lod == 0 ? 7 : 4;
                for (int i = 0; i < n; i++)
                {
                    double y = 9.0 + (c0 - 9.0) * (i + 0.5 * ShapeNoise.Unit(i, 1, 3, seed)) / n, ang = 2.4 * i + seed;
                    double r = 0.5 + 0.4 * (0.5 + 0.5 * ShapeNoise.Unit(i, 2, 3, seed));
                    Shapes.Ellipsoid(m, SacredDraw.Turn(x, 0.45 * r * Math.Cos(ang), y, 0.45 * r * Math.Sin(ang), ang), Needles, r, 0.45, 0.6 * r, seg, false);
                }
            }
            // The crown: clumps in two rings and a top, wider than tall, the top flattish.
            int clumps = lod == 0 ? 9 : lod == 1 ? 7 : 4;
            for (int i = 0; i < clumps; i++)
            {
                bool upper = i >= clumps / 2;
                double ang = 2 * Math.PI * i / Math.Max(1, clumps / 2) + (upper ? 0.6 : 0) + ShapeNoise.Unit(i, 4, 5, seed) * 0.4;
                double rr = upper ? 1.3 : 2.0, y = c0 + (upper ? 5.6 : 3.2) + 0.6 * ShapeNoise.Unit(i, 5, 5, seed);
                double s = 1.2 + 0.35 * (0.5 + 0.5 * ShapeNoise.Unit(i, 6, 5, seed));
                ShapeBrush br = i % 3 == 0 ? NeedlesLit : Needles;
                Shapes.Ellipsoid(m, SacredDraw.Turn(x, rr * Math.Cos(ang), y, rr * Math.Sin(ang), ang), br, 1.25 * s, 0.8 * s, s, lod == 0 ? 2 : seg, lod == 0);
            }
            Shapes.Ellipsoid(m, SacredDraw.At(x, 0, H - 1.1, 0), NeedlesLit, 1.6, 0.9, 1.6, lod == 0 ? 2 : seg, lod == 0);
            f.CylinderClear(a, b, g, g + 4, 0.4);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Builders
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A two-tier Newar temple at site (ca, cb) turned by yaw, to its <see cref="TwoTierSpec"/>, with its
        /// colliders (no AO bake: the callers bake).</summary>
        private static void TwoTier(ref PashupatiFrame f, MeshData m, double ca, double cb, double yaw, in TwoTierSpec p)
        {
            int lod = f.Lod;
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            Affine3 k = f.Facing(ca, 0, cb, -sy, cy);
            if (lod >= 3)
            {
                PashupatiKit.Block(m, k, -p.WallU, p.WallU, -0.2, p.CorniceTop, -p.WallW, p.WallW, p.Wall);
                FarRoof(m, k, p.WallU + 0.2, p.WallW + 0.2, p.CorniceTop, p.Eave1U, p.Eave1W, p.Eave1V, p.BodyU, p.BodyW, p.Top1V, p.Roof);
                PashupatiKit.Block(m, k, -p.BodyU, p.BodyU, p.Top1V - 0.3, p.BodyTop, -p.BodyW, p.BodyW, PashupatiKit.WoodDark);
                FarRoof(m, k, p.BodyU + 0.15, p.BodyW + 0.15, p.BodyTop, p.Eave2U, p.Eave2W, p.Eave2V, p.TopHalf, p.TopHalf, p.TopV, p.Roof);
                Shapes.Cone(m, SacredDraw.At(k, 0, p.TopV, 0), PashupatiKit.GiltBright, 1.3 * p.FinialR, p.FinialH, 4, 0, 0, false);
                return;
            }
            // Plinth and the steps at the door.
            PashupatiKit.Band(m, k, 0, 0, p.PlinthU, p.PlinthW,
                              lod >= 2
                                  ? new[] { 0.0, -0.2, 0.0, p.PlinthH }
                                  : new[] { 0.04, -0.2, 0.04, 0.08, 0.0, 0.12, 0.0, p.PlinthH - 0.08, 0.05, p.PlinthH - 0.05, 0.05, p.PlinthH },
                              PashupatiKit.StonePlinth, true, Flag);
            {
                Affine3 st = Face(k, p.DoorSide, p.PlinthU, p.PlinthW);
                int steps = Math.Max(1, (int)Math.Round(p.PlinthH / 0.2)) - 1;
                double rise = p.PlinthH / (steps + 1);
                for (int i = 0; i < steps; i++)
                    SacredDraw.Box(m, st, -0.8, 0.8, 0, p.PlinthH - (i + 1) * rise, i * 0.3 - 0.02, (i + 1) * 0.3, PashupatiKit.StoneEdge,
                                   BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
            }
            // Ground storey: the wall with a carved wooden band under the cornice.
            Mould w = SacredDraw.M;
            w.Add(0, p.PlinthH - 0.02, p.Wall).Add(0, p.WallTop - 0.32, PashupatiKit.WoodDark).Add(0, p.WallTop);
            SacredDraw.RectRing(m, k, 0, 0, p.WallU, p.WallW, w, false, PashupatiKit.WoodDark);
            double storey = p.WallTop - 0.32 - p.PlinthH;
            for (int s = 0; s < 4; s++)
            {
                double half = s % 2 == 0 ? p.WallU : p.WallW;
                Affine3 x = Face(k, s, p.WallU, p.WallW);
                if (s == p.DoorSide)
                    SacredParts.Door(m, x, 0, p.PlinthH, 0, Math.Min(1.15, 0.55 * half), Math.Min(2.1, storey - 0.7), PashupatiKit.WoodDark, lod, true, p.WallTop - 0.34);
                else if (lod <= 1)
                    SacredParts.LatticeWindow(m, x, 0, p.PlinthH + 0.56 * storey, 0, Math.Min(0.85, 0.42 * half), Math.Min(0.95, 0.4 * storey), PashupatiKit.WoodDark, lod,
                                              3);
                if (lod == 0)
                {
                    // Corner pilasters of carved wood.
                    SacredDraw.Box(m, x, -half - 0.02, -half + 0.16, p.PlinthH, p.WallTop - 0.32, 0, 0.06, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Right);
                    SacredDraw.Box(m, x, half - 0.16, half + 0.02, p.PlinthH, p.WallTop - 0.32, 0, 0.06, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Left);
                }
            }
            PashupatiKit.Band(m, k, 0, 0, p.WallU, p.WallW, new[] { 0.0, p.WallTop, 0.14, p.WallTop + 0.05, 0.14, p.CorniceTop - 0.08, 0.22, p.CorniceTop },
                              PashupatiKit.WoodDark, true, PashupatiKit.WoodDark);

            // Lower tier.
            double fascia = 0.16 + 0.025 * p.Eave1U;
            PashupatiKit.Skirt(m, k, p.WallU + 0.22, p.WallW + 0.22, p.CorniceTop, p.Eave1U - 0.05, p.Eave1W - 0.05, p.Eave1V - fascia, 0.2, lod);
            PashupatiKit.Struts(m, k, p.WallU, p.WallW, p.CorniceTop - 0.1, p.Eave1U, p.Eave1W, p.Eave1V - fascia, p.Struts1, p.Struts1, 0.84, p.Seed, lod);
            var lower = new PRoof
            {
                EaveU = p.Eave1U, EaveW = p.Eave1W, EaveV = p.Eave1V, TopU = p.BodyU + 0.1, TopW = p.BodyW + 0.1, TopV = p.Top1V, Lift = p.Lift, FasciaH = fascia,
                SeamPitch = p.SeamPitch, CurlSize = p.Curl, Roof = p.Roof, Seam = p.Rib, Fascia = p.Fascia, Curl = p.CurlB,
            };
            PashupatiKit.Roof(m, k, lower, lod);

            // Upper body under the upper cornice, small lattice windows.
            Mould ub = SacredDraw.M;
            ub.Add(0, p.Top1V - 0.3, p.Body).Add(0, p.BodyTop - 0.24, PashupatiKit.WoodDark).Add(0.1, p.BodyTop - 0.2).Add(0.1, p.BodyTop);
            SacredDraw.RectRing(m, k, 0, 0, p.BodyU, p.BodyW, ub, true, PashupatiKit.WoodDark);
            if (lod <= 1)
            {
                double bh = p.BodyTop - 0.24 - p.Top1V;
                for (int s = 0; s < 4; s++)
                {
                    double half = s % 2 == 0 ? p.BodyU : p.BodyW;
                    SacredParts.LatticeWindow(m, Face(k, s, p.BodyU, p.BodyW), 0, p.Top1V + 0.55 * bh, 0, Math.Min(0.7, 0.5 * half), Math.Min(0.45, 0.6 * bh),
                                              PashupatiKit.WoodDark, lod, 2);
                }
            }
            // Upper tier and the top.
            double uf = 0.14 + 0.025 * p.Eave2U;
            PashupatiKit.Skirt(m, k, p.BodyU + 0.2, p.BodyW + 0.2, p.BodyTop, p.Eave2U - 0.05, p.Eave2W - 0.05, p.Eave2V - uf, 0.2, lod);
            PashupatiKit.Struts(m, k, p.BodyU, p.BodyW, p.BodyTop - 0.08, p.Eave2U, p.Eave2W, p.Eave2V - uf, p.Struts2, p.Struts2, 0.84, p.Seed + 50u, lod + 1);
            var upper = new PRoof
            {
                EaveU = p.Eave2U, EaveW = p.Eave2W, EaveV = p.Eave2V, TopU = p.TopHalf, TopW = p.TopHalf, TopV = p.TopV, Lift = 0.8 * p.Lift, FasciaH = uf,
                SeamPitch = p.SeamPitch, CurlSize = 0.8 * p.Curl, Roof = p.Roof, Seam = p.Rib, Fascia = p.Fascia, Curl = p.CurlB,
            };
            PashupatiKit.Roof(m, k, upper, lod);
            PashupatiKit.Band(m, k, 0, 0, p.TopHalf, p.TopHalf, new[] { -0.02, p.TopV - 0.04, 0.03, p.TopV, 0.03, p.TopV + 0.08 }, p.Fascia, true, p.Roof);
            PashupatiKit.BellFinial(m, k, 0, p.TopV + 0.08, 0, p.FinialH - 0.08, p.FinialR, PashupatiKit.GiltBright, lod);

            // Colliders: the walkable plinth, the closed body.
            f.BoxAlong(ca, cb, p.PlinthU, p.PlinthW, cy, sy, -0.2, p.PlinthH, GenColliderFlags.Walkable, GenColliders.Stone);
            f.BoxAlong(ca, cb, p.WallU, p.WallW, cy, sy, p.PlinthH, p.Eave1V, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
        }

        /// <summary>The far (LOD3) form of one tier: the black band from the wall to the eave and the roof's four slopes.</summary>
        private static void FarRoof(MeshData m, in Affine3 k, double wu, double ww, double wv, double eu, double ew, double ev, double tu, double tw, double tv,
                                    in ShapeBrush roof)
        {
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, e0u, e0w, e1u, e1w, cu, cw, du, dw, ou, ow;
                PashupatiKit.OnRect(s, -1, wu, ww, out au, out aw);
                PashupatiKit.OnRect(s, 1, wu, ww, out bu, out bw);
                PashupatiKit.OnRect(s, -1, eu, ew, out e0u, out e0w);
                PashupatiKit.OnRect(s, 1, eu, ew, out e1u, out e1w);
                PashupatiKit.OnRect(s, 1, tu, tw, out cu, out cw);
                PashupatiKit.OnRect(s, -1, tu, tw, out du, out dw);
                PashupatiKit.Out(s, out ou, out ow);
                SacredDraw.Quad(m, k, au, wv, aw, bu, wv, bw, e1u, ev - 0.2, e1w, e0u, ev - 0.2, e0w, ou, -0.6, ow, PashupatiKit.Lattice);
                SacredDraw.Quad(m, k, e0u, ev - 0.2, e0w, e1u, ev - 0.2, e1w, e1u, ev, e1w, e0u, ev, e0w, ou, 0, ow, roof);
                SacredDraw.Quad(m, k, e0u, ev, e0w, e1u, ev, e1w, cu, tv, cw, du, tv, dw, ou, 0.8, ow, roof);
            }
        }

        /// <summary>A regular polygon of circumradius r, its first corner at angle <paramref name="rot"/>
        /// (counter-clockwise in local u, w).</summary>
        private static int Poly(int n, double r, double rot, double[] pu, double[] pw)
        {
            for (int i = 0; i < n; i++)
            {
                double t = rot + 2 * Math.PI * i / n;
                pu[i] = r * Math.Cos(t);
                pw[i] = r * Math.Sin(t);
            }
            return n;
        }

        /// <summary>A cross plan (a square of half size h with a central projection of half width pr standing out dp
        /// on each face), counter-clockwise; returns 20.</summary>
        private static int Cross(double h, double pr, double dp, double[] pu, double[] pw)
        {
            int n = 0;
            for (int s = 0; s < 4; s++)
            {
                for (int q = 0; q < 5; q++)
                {
                    // The +u face's five points: (h, −h), (h, −pr), (h + dp, −pr), (h + dp, pr), (h, pr).
                    double u = q == 2 || q == 3 ? h + dp : h, w = q == 0 ? -h : q <= 2 ? -pr : pr;
                    // Turned by s quarter turns: (u, w) → (−w, u).
                    for (int r = 0; r < s; r++)
                    {
                        double t = u;
                        u = -w;
                        w = t;
                    }
                    pu[n] = u;
                    pw[n] = w;
                    n++;
                }
            }
            return n;
        }

        /// <summary>A tower over a counter-clockwise plan from v0 to v1 in <paramref name="stations"/> courses, the plan
        /// scaled about the axis from 1 to <paramref name="topScale"/> along a convex curve (the shikhara's bulge); courses
        /// alternate the two looks; flat facets.</summary>
        private static void Tower(MeshData m, in Affine3 k, double[] pu, double[] pw, int n, double v0, double v1, int stations, double topScale, in ShapeBrush b,
                                  in ShapeBrush groove)
        {
            for (int s = 0; s < stations; s++)
            {
                double t0 = (double)s / stations, t1 = (double)(s + 1) / stations;
                double s0 = 1 - (1 - topScale) * Math.Pow(t0, 1.4), s1 = 1 - (1 - topScale) * Math.Pow(t1, 1.4);
                double va = v0 + (v1 - v0) * t0, vb = v0 + (v1 - v0) * t1;
                ShapeBrush br = s % 2 == 0 ? b : groove;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double eu = pu[j] - pu[i], ew = pw[j] - pw[i];
                    double du = pu[i] * (s1 - s0), dv = vb - va, dw = pw[i] * (s1 - s0);
                    // Outward normal of a counter-clockwise plan: d × e.
                    double nx = ew * dv, ny = eu * dw - ew * du, nz = -eu * dv;
                    SacredDraw.Quad(m, k, pu[i] * s0, va, pw[i] * s0, pu[j] * s0, va, pw[j] * s0, pu[j] * s1, vb, pw[j] * s1, pu[i] * s1, vb, pw[i] * s1, nx, ny, nz, br);
                }
            }
        }

        /// <summary>A fluted (ribbed) dome or bell from a (radius, height) profile, <paramref name="flutes"/> ribs of
        /// relative depth <paramref name="amp"/> (smooth at LOD2).</summary>
        private static void Fluted(MeshData m, in Affine3 xf, in ShapeBrush b, double[] rh, int flutes, double amp, int lod)
        {
            int cols = lod <= 0 ? 2 * flutes : lod == 1 ? flutes : 8;
            double a = lod >= 2 ? 0 : amp;
            int rows = rh.Length / 2, first = m.VertexCount, i0 = m.IndexCount;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    double th = 2 * Math.PI * c / cols;
                    double rr = rh[2 * r] * (1 - a * (0.5 - 0.5 * Math.Cos(flutes * th)));
                    SacredDraw.V(m, xf, rr * Math.Sin(th), rh[2 * r + 1], rr * Math.Cos(th), Math.Sin(th), 0.4, Math.Cos(th), b);
                }
            }
            SacredDraw.Grid(m, first, rows, cols, true, null);
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, i0, m.IndexCount - i0, false);
        }

        /// <summary>A polygonal hipped roof: facets from the eave polygon (circumradius rE at vE, the corners lifted) to
        /// the top polygon (rT at vT), the fascia under the eave, hip rolls (LOD0-1) and standing seams (LOD0).</summary>
        private static void PolyRoof(MeshData m, in Affine3 k, int n, double rot, double rE, double vE, double rT, double vT, double lift, double fasciaH,
                                     in ShapeBrush roof, in ShapeBrush rib, in ShapeBrush fascia, int lod)
        {
            double cE = Math.Cos(Math.PI / n), ev = vE + lift;
            for (int i = 0; i < n; i++)
            {
                double t0 = rot + 2 * Math.PI * i / n, t1 = rot + 2 * Math.PI * (i + 1) / n, tm = 0.5 * (t0 + t1);
                double c0 = Math.Cos(t0), s0 = Math.Sin(t0), c1 = Math.Cos(t1), s1 = Math.Sin(t1), cm = Math.Cos(tm), sm = Math.Sin(tm);
                double e0u = rE * c0, e0w = rE * s0, e1u = rE * c1, e1w = rE * s1, emu = rE * cE * cm, emw = rE * cE * sm;
                double p0u = rT * c0, p0w = rT * s0, p1u = rT * c1, p1w = rT * s1, pmu = rT * cE * cm, pmw = rT * cE * sm;
                Facet(m, k, e0u, ev, e0w, emu, vE, emw, pmu, vT, pmw, p0u, vT, p0w, cm, sm, roof);
                Facet(m, k, emu, vE, emw, e1u, ev, e1w, p1u, vT, p1w, pmu, vT, pmw, cm, sm, roof);
                SacredDraw.Quad(m, k, e0u, ev, e0w, emu, vE, emw, emu, vE - fasciaH, emw, e0u, ev - fasciaH, e0w, cm, 0, sm, fascia);
                SacredDraw.Quad(m, k, emu, vE, emw, e1u, ev, e1w, e1u, ev - fasciaH, e1w, emu, vE - fasciaH, emw, cm, 0, sm, fascia);
                if (lod <= 1)
                {
                    Path3 h = PashupatiKit.PathS;
                    h.Add(e0u, ev + 0.04, e0w).Add(p0u, vT + 0.04, p0w);
                    Shapes.Tube(m, k, rib, h, 0.06, 4, true);
                }
                if (lod > 0) continue;
                // Standing seams: two on each half of the facet, down the fall line.
                for (int half = 0; half < 2; half++)
                {
                    double au = half == 0 ? e0u : emu, aw = half == 0 ? e0w : emw, av = half == 0 ? ev : vE;
                    double bu = half == 0 ? emu : e1u, bw = half == 0 ? emw : e1w, bv = half == 0 ? vE : ev;
                    double tu0 = half == 0 ? p0u : pmu, tw0 = half == 0 ? p0w : pmw, tu1 = half == 0 ? pmu : p1u, tw1 = half == 0 ? pmw : p1w;
                    for (int q = 1; q <= 2; q++)
                    {
                        double t = q / 3.0;
                        Shapes.Bar(m, k, rib, au + (bu - au) * t, av + (bv - av) * t + 0.03, aw + (bw - aw) * t, tu0 + (tu1 - tu0) * t, vT + 0.02, tw0 + (tw1 - tw0) * t,
                                   0.03, 3);
                    }
                }
            }
        }

        private static void Facet(MeshData m, in Affine3 k, double au, double av, double aw, double bu, double bv, double bw, double cu, double cv, double cw, double du,
                                  double dv, double dw, double ou, double ow, in ShapeBrush br)
        {
            double xu = bu - au, xv = bv - av, xw = bw - aw, yu = du - au, yv = dv - av, yw = dw - aw;
            double nx = xv * yw - xw * yv, ny = xw * yu - xu * yw, nz = xu * yv - xv * yu;
            if (nx * ou + ny * 0.5 + nz * ow < 0)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            SacredDraw.Quad(m, k, au, av, aw, bu, bv, bw, cu, cv, cw, du, dv, dw, nx, ny, nz, br);
        }

        /// <summary>The black lattice skirt of a polygonal tier, from the wall polygon (rW at vW) out to under the eave
        /// (rE at vE).</summary>
        private static void PolySkirt(MeshData m, in Affine3 k, int n, double rot, double rW, double vW, double rE, double vE)
        {
            double dOut = rE - rW, dUp = vE - vW, l = Math.Sqrt(dOut * dOut + dUp * dUp);
            for (int i = 0; i < n; i++)
            {
                double t0 = rot + 2 * Math.PI * i / n, t1 = rot + 2 * Math.PI * (i + 1) / n, tm = 0.5 * (t0 + t1);
                double c0 = Math.Cos(t0), s0 = Math.Sin(t0), c1 = Math.Cos(t1), s1 = Math.Sin(t1), cm = Math.Cos(tm), sm = Math.Sin(tm);
                SacredDraw.Quad(m, k, rW * c0, vW, rW * s0, rW * c1, vW, rW * s1, rE * c1, vE, rE * s1, rE * c0, vE, rE * s0, cm * dUp / l, -dOut / l, sm * dUp / l,
                                PashupatiKit.Lattice);
            }
        }

        /// <summary>One plain strut per face of a polygonal tier (LOD0), from the cornice to under the skirt.</summary>
        private static void PolyStruts(MeshData m, in Affine3 k, int n, double rot, double rW, double vW, double rE, double vE)
        {
            double ap = Math.Cos(Math.PI / n);
            for (int i = 0; i < n; i++)
            {
                double tm = rot + 2 * Math.PI * (i + 0.5) / n, cm = Math.Cos(tm), sm = Math.Sin(tm);
                double r0 = rW * ap + 0.1, r1 = rW * ap + 0.78 * (rE - rW) * ap, v1 = vW + 0.78 * (vE - vW) - 0.12;
                Shapes.Bar(m, k, PashupatiKit.StrutWood, r0 * cm, vW, r0 * sm, r1 * cm, v1, r1 * sm, 0.07, 4);
            }
        }
    }
}
