using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>Window kinds of the courtyard's buildings.</summary>
    internal enum WinStyle : byte
    {
        /// <summary>A carved dark-wood (or red-framed) lattice window (Newar and Rana-period houses).</summary>
        Lattice = 0,

        /// <summary>A tall round-arched window with a red frame (the white riverside wings).</summary>
        Arched = 1,

        /// <summary>A plain dark window with a sill.</summary>
        Plain = 2,
    }

    /// <summary>Roof kinds of the courtyard's buildings.</summary>
    internal enum RoofKind : byte
    {
        /// <summary>Flat roof terrace inside a parapet.</summary>
        Flat = 0,

        /// <summary>Hipped corrugated-metal roof over the outline's oriented box.</summary>
        Hip = 1,

        /// <summary>Gabled corrugated-metal roof over the outline's oriented box.</summary>
        Gable = 2,
    }

    /// <summary>One building of the ring round the courtyard: its OSM outline (site metres), wall foot and first floor,
    /// storeys, colours, windows and roof.</summary>
    internal struct House
    {
        public double[] Plan;
        public double Bottom, Floor, StoreyM, WindowPitch, Overhang, Pitch, Parapet, DadoM, MinWall;
        public int Storeys;
        public ShapeBrush Wall, Band, Frame, RoofB, RibB, Dado;
        public WinStyle Windows;
        public RoofKind Roof;
        public uint Seed;

        /// <summary>No windows in the ground storey (its walls carry painted or carved work instead).</summary>
        public bool BlindGround;
    }

    /// <summary>
    /// The buildings that close the Pashupatinath courtyard (pashupati-temple.md PT-04 flanks, PT-08, PT-11, PT-12,
    /// PT-13, PT-19, PT-21): white, cream and red-brick houses of two or three storeys with lattice or round-arched windows
    /// and pitched rust-red (or grey) corrugated metal roofs, on their OSM outlines; the Mukti Mandap's dark arcaded
    /// pavilion on its white riverside block with the red-and-white crenellated parapet; the white east wing stepping down
    /// to the river with its arched windows and wooden balcony; the Newar brick South Gate range with its carved door. None
    /// is taller than the temple's upper eave, as the photos show (the old blocks were 4-6 storey candy-coloured cubes).
    /// </summary>
    internal static class PashupatiRing
    {
        /// <summary>Heights above the courtyard of the parts' highest points (east wing parapet, Mukti Mandap finial).</summary>
        public const float EastWingTopM = 7.6f, MuktiTopM = 10.4f;

        [ThreadStatic] private static double[] _pa, _pb;

        // OSM outlines in site metres (a, b), from the region pack (kathmandu_core, 2026-10-04 snapshot).
        // The West Gate stands on the temple axis (b −4.8..+4.8, PashupatiGates.WestB): the buildings either side of it are
        // trimmed back from it (their OSM outlines overlap the gatehouse mapped 3.45 m south) or carried up to it, so white
        // walls abut the gate on both sides as in every photo (c_westgate/c_00, c_03, c_09) and no slot opens beside it.
        private static readonly double[] NorthFlank = { -42.6, 18.4, -49.6, 17.8, -49.0, 5.4, -41.7, 6.1 }; // r3442318
        private static readonly double[] NorthLink = { -41.75, 4.85, -34.75, 4.85, -34.75, 5.4, -41.7, 6.1 }; // the wall from r3442318 to the gate
        private static readonly double[] SouthFlank = { -33.6, -15.8, -34.2, -4.85, -39.9, -4.85, -38.6, -16.3 }; // w913170311, carried up to the gate
        private static readonly double[] InnerSouth = { -23.2, -13.9, -23.4, -3.2, -33.3, -3.2, -31.6, -14.2 }; // w913170313, to the gate's courtyard face
        private static readonly double[] InnerNorth = { -35.4, 16.4, -34.7, 4.85, -29.3, 5.7, -30.0, 16.8 }; // w913170308, down to the gate
        private static readonly double[] GoldenDoorHouse = { -39.3, -8.43, -46.03, -8.96, -45.51, -16.0, -38.74, -16.0 }; // w913170310, north part
        private static readonly double[] SouthWest = { -38.74, -16.0, -45.51, -16.0, -44.51, -29.54, -37.79, -29.0 }; // w913170310, south part
        private static readonly double[] SouthWest2 = { -34.7, -31.3, -34.8, -29.1, -31.6, -29.0, -31.9, -16.1, -38.6, -16.3, -37.8, -29.0, -37.6, -31.4 }; // w913170312
        private static readonly double[] North = { -30.0, 16.8, -4.4, 18.5, -4.9, 27.9, -35.9, 25.8, -35.4, 16.4 }; // w913170309
        // The houses north of the courtyard behind the Araucarias (w913217415, an L split into its two legs, and w913217414).
        private static readonly double[] NorthHouseW = { 8.2, 33.9, 14.3, 34.7, 12.3, 48.9, 6.1, 49.0 }; // w913217415, south leg
        private static readonly double[] NorthHouseN = { 6.1, 49.0, 12.3, 48.9, 23.9, 50.5, 24.7, 56.1, 5.5, 53.4 }; // w913217415, north leg
        private static readonly double[] NorthHouseE = { 17.0, 40.3, 22.0, 41.0, 21.4, 45.3, 16.4, 44.5 }; // w913217414
        private static readonly double[] South = { -19.1, -25.1, -2.5, -23.3, -3.4, -14.8, -26.4, -17.3, -25.5, -25.9 }; // w913170317
        private static readonly double[] Mulbhatt = { -22.2, -39.2, -16.0, -37.6, -19.1, -25.1, -25.5, -25.9 }; // w913170301
        private static readonly double[] Priests2 = { -48.8, -41.9, -43.1, -40.6, -42.8, -42.0, -38.5, -41.1, -38.1, -42.9, -22.7, -39.5, -24.5, -31.3, -38.3, -34.3, -38.9, -31.2, -50.5, -33.8 }; // w913170302
        private static readonly double[] Priests3 = { -54.1, -31.6, -45.3, -29.6, -46.6, -24.1, -50.8, -25.1, -52.3, -18.5, -56.8, -19.5 }; // w913170306
        private static readonly double[] SouthEast = { 14.8, -21.2, 21.9, -20.9, 22.1, -26.1, 26.3, -25.8, 32.6, -25.5, 32.0, -13.1, 14.5, -14.0 }; // w913217411
        private static readonly double[] EastWingPlan =
        {
            26.2, 9.6, 31.5, 10.6, 31.2, 13.1, 33.4, 13.4, 32.2, 25.1, 31.8, 26.8, 29.7, 26.6, 29.2, 31.1, 25.5, 30.7, 25.2, 33.4, 15.6, 32.3, 16.6, 22.8, 17.3, 22.9,
            18.85, 9.45,
        }; // w913217416 (with part w1033128669), its gatehouse part south of b 9.5 left to the East Gate's own passage

        private static readonly ShapeBrush WhitePlaster = PashupatiKit.White;
        private static readonly ShapeBrush MuktiGrey = PashupatiKit.Br(0x8E979E, MaterialChannel.Metal);
        private static readonly ShapeBrush MuktiRib = PashupatiKit.Br(0xAAB2B8, MaterialChannel.Metal);
        private static readonly ShapeBrush CreamPlaster = PashupatiKit.Br(0xEDE2C4, MaterialChannel.Plaster);
        private static readonly ShapeBrush YellowPlaster = PashupatiKit.Br(0xE6D29A, MaterialChannel.Plaster);
        private static readonly ShapeBrush BandWhite = PashupatiKit.Br(0xFAF8F2, MaterialChannel.Plaster);
        private static readonly ShapeBrush BrickRed = PashupatiKit.Br(0xA4553A, MaterialChannel.Brick);
        private static readonly ShapeBrush BrickGlazed = PashupatiKit.Br(0x8E3F2C, MaterialChannel.BrickGlazed);
        private static readonly ShapeBrush GreyRoof = PashupatiKit.Br(0x8A9196, MaterialChannel.Metal);
        private static readonly ShapeBrush GreyRoofRib = PashupatiKit.Br(0xA6ADB2, MaterialChannel.Metal);
        private static readonly ShapeBrush WindowRed = PashupatiKit.Br(0x9E2F25, MaterialChannel.Paint);
        private static readonly ShapeBrush WindowWood = PashupatiKit.Br(0x3A2518, MaterialChannel.WoodCarved);
        private static readonly ShapeBrush Glass = PashupatiKit.Br(0x2B2826, MaterialChannel.Plain, 0.7f);
        private static readonly ShapeBrush PlinthStone = PashupatiKit.Br(0x7E796F, MaterialChannel.Stone);
        private static readonly ShapeBrush ParapetRed = PashupatiKit.Br(0x9E2F25, MaterialChannel.Paint);
        private static readonly ShapeBrush ParapetCream = PashupatiKit.Br(0xE6DCC4, MaterialChannel.Plaster);

        // ---------------------------------------------------------------------------------------------------------
        // The buildings
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The west group (PT-04 flanks, PT-05 frontages): the white three-storey Rana-period house north of the
        /// forecourt with its rust-red gable roof (r3442318) and the white wall linking it to the gate; directly south of
        /// the gate the white two-storey golden-door house (the north part of w913170310: the gilt door between painted
        /// eyes on its forecourt face, the red LED band, a rust roof with a dormer; c_westgate/c_03, c_00) and beyond it the
        /// red-brick Newar house with carved windows (its south part, c_westgate/c_31); the two-storey houses between the
        /// gate and the courtyard and round the forecourt.</summary>
        public static void WestBuildings(ref PashupatiFrame f, MeshData m)
        {
            double fr = PashupatiSite.ForecourtRiseM;
            Build(ref f, m, Plastered(NorthFlank, fr, 3, 2.9, WhitePlaster, WinStyle.Lattice, RoofKind.Gable, PashupatiKit.RustRoof, PashupatiKit.RustRib, 41u));
            House link = Plastered(NorthLink, fr, 2, 3.0, WhitePlaster, WinStyle.Lattice, RoofKind.Flat, default, default, 47u);
            link.Parapet = 0.5;
            Build(ref f, m, link);
            House sf = Plastered(SouthFlank, fr, 2, 3.0, WhitePlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof, PashupatiKit.RustRib, 42u);
            Build(ref f, m, sf);
            House gd = Plastered(GoldenDoorHouse, fr, 2, 3.3, WhitePlaster, WinStyle.Plain, RoofKind.Hip, PashupatiKit.RustRoof, PashupatiKit.RustRib, 48u);
            gd.Frame = PashupatiKit.WoodDark;
            // The ground storey's white wall carries the gilt door and the painted eyes, no windows (c_westgate/c_03).
            gd.BlindGround = true;
            Build(ref f, m, gd);
            GoldenDoor(ref f, m);
            House isn = Plastered(InnerSouth, 0, 2, 3.0, CreamPlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof, PashupatiKit.RustRib, 43u);
            isn.Dado = PashupatiKit.Br(0xA8322A, MaterialChannel.Paint);
            isn.DadoM = 1.3;
            Build(ref f, m, isn);
            Build(ref f, m, Plastered(InnerNorth, 0, 2, 3.0, WhitePlaster, WinStyle.Lattice, RoofKind.Hip, GreyRoof, GreyRoofRib, 44u));
            House sw = Plastered(SouthWest, fr, 3, 2.8, BrickRed, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof, PashupatiKit.RustRib, 45u);
            sw.Band = PashupatiKit.WoodDark;
            Build(ref f, m, sw);
            Build(ref f, m, Plastered(SouthWest2, fr, 2, 3.0, CreamPlaster, WinStyle.Plain, RoofKind.Flat, default, default, 46u));
        }

        /// <summary>PT-12, the north wing (w913170309): three storeys of white lime plaster with lattice windows under a long
        /// red corrugated hip roof, its south face closing the courtyard 6 m from the temple.</summary>
        public static void NorthWing(ref PashupatiFrame f, MeshData m)
        {
            Build(ref f, m, Plastered(North, 0, 3, 2.85, WhitePlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof, PashupatiKit.RustRib, 51u));
            // North of the courtyard, round the Araucarias, the photos show cream and white plastered houses of two and
            // three storeys under rust-red roofs (roof/ref_07, complex/ref_09, c_gje/c_12), where the house bands drew one
            // saturated 13 m block (w913217414, w913217415).
            Build(ref f, m, Plastered(NorthHouseW, Ground(ref f, NorthHouseW), 3, 2.9, CreamPlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof,
                                      PashupatiKit.RustRib, 52u));
            Build(ref f, m, Plastered(NorthHouseN, Ground(ref f, NorthHouseN), 3, 2.8, WhitePlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof,
                                      PashupatiKit.RustRib, 53u));
            Build(ref f, m, Plastered(NorthHouseE, Ground(ref f, NorthHouseE), 2, 3.0, CreamPlaster, WinStyle.Plain, RoofKind.Gable, PashupatiKit.RustRoof,
                                      PashupatiKit.RustRib, 54u));
        }

        /// <summary>PT-13, the South Gate range (w913170317): a two-storey Newar building of red facing brick with a
        /// projecting wooden cornice, carved struts under its long tiled eave and carved windows, and on its outer (south)
        /// face the tall carved door under its torana with red-painted lions on brick plinths.</summary>
        public static void SouthRange(ref PashupatiFrame f, MeshData m)
        {
            House h = Plastered(South, 0, 2, 3.2, BrickRed, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.Br(0x8E4A36, MaterialChannel.RoofTile),
                                PashupatiKit.Br(0x6E3626, MaterialChannel.RoofTile), 61u);
            h.Band = PashupatiKit.WoodDark;
            h.Frame = WindowWood;
            h.Overhang = 1.3;
            Build(ref f, m, h);
            if (f.Lod >= 2) return;
            // The carved door on the south face (the edge from (−19.1, −25.1) to (−2.5, −23.3) faces 216.7°).
            double ma = -10.8, mb = -24.2, ea = -2.5 - -19.1, eb = -23.3 - -25.1, el = Math.Sqrt(ea * ea + eb * eb);
            double na = eb / el, nb = -ea / el;
            Affine3 x = f.Facing(ma + 0.02 * na, 0, mb + 0.02 * nb, na, nb);
            SacredDraw.Box(m, x, -1.1, 1.1, 0, 4.0, 0, 0.1, PashupatiKit.Br(0x2E2018, MaterialChannel.WoodCarved), BoxFaces.Front);
            SacredParts.Door(m, x, 0, 0.0, 0.1, 2.0, 3.7, WindowWood, f.Lod, true);
            for (int s = -1; s <= 1; s += 2)
            {
                SacredDraw.Box(m, x, s * 1.6 - 0.35, s * 1.6 + 0.35, 0, 0.7, 0, 0.7, BrickRed, BoxFaces.Wall);
                if (f.Lod == 0)
                {
                    int first = m.VertexCount;
                    SacredFigures.Guardian(m, x, s * 1.6, 0.7, 0.35, 1.0, GuardianKind.Lion, true, SacredFigures.Small, f.Lod, s);
                    ShapeColor.Tint(m, first, m.VertexCount - first, 0xC0402EFFu);
                }
            }
        }

        /// <summary>PT-19, the priests' quarters to the south-west (Mul Bhatta Niwas w913170301, w913170302, w913170306):
        /// two- and three-storey white and cream plastered houses with lattice windows and rust-red or grey roofs.</summary>
        public static void PriestsQuarters(ref PashupatiFrame f, MeshData m)
        {
            Build(ref f, m, Plastered(Mulbhatt, Ground(ref f, Mulbhatt), 3, 2.8, YellowPlaster, WinStyle.Lattice, RoofKind.Hip, PashupatiKit.RustRoof,
                                      PashupatiKit.RustRib, 71u));
            Build(ref f, m, Plastered(Priests2, Ground(ref f, Priests2), 2, 3.0, WhitePlaster, WinStyle.Lattice, RoofKind.Flat, default, default, 72u));
            Build(ref f, m, Plastered(Priests3, Ground(ref f, Priests3), 3, 2.8, CreamPlaster, WinStyle.Plain, RoofKind.Flat, default, default, 73u));
        }

        /// <summary>PT-11's east wing (w913217416 with its part w1033128669): a long white lime-plastered building of two
        /// storeys over the courtyard and four over the ghat walk, rows of tall round-arched windows with red frames, a dark
        /// wooden balcony under a red awning roof along the river face, the ghat-level arcade and white parapets.</summary>
        public static void EastWing(ref PashupatiFrame f, MeshData m)
        {
            double foot = Math.Min(-2.5, f.GroundAt(34.5, 18.0, -4.2));
            House h = Plastered(EastWingPlan, 0, 2, 3.4, WhitePlaster, WinStyle.Arched, RoofKind.Flat, default, default, 81u);
            h.Bottom = foot - 0.3;
            h.Floor = foot;
            h.Storeys = 4;
            h.StoreyM = (EastWingTopM - 0.9 - foot) / 4;
            h.Frame = WindowRed;
            h.Parapet = 0.9;
            Build(ref f, m, h);
            if (f.Lod >= 2) return;
            // The wooden balcony with its red awning roof along the river face (the edge (33.4, 13.4) → (32.2, 25.1)).
            double a0 = 33.4, b0 = 13.4, a1 = 32.2, b1 = 25.1, ea = a1 - a0, eb = b1 - b0, el = Math.Sqrt(ea * ea + eb * eb);
            double na = eb / el, nb = -ea / el, ma = 0.5 * (a0 + a1), mb = 0.5 * (b0 + b1);
            Affine3 x = f.Facing(ma, 0, mb, na, nb);
            double hb = foot + 2 * h.StoreyM, half = 0.5 * el - 0.6;
            SacredDraw.Box(m, x, -half, half, hb - 0.15, hb, 0, 1.2, PashupatiKit.WoodDark, BoxFaces.All & ~BoxFaces.Back);
            SacredDraw.Box(m, x, -half, half, hb, hb + 1.0, 1.1, 1.2, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Top);
            if (f.Lod == 0)
            {
                int posts = (int)Math.Round(2 * half / 1.2);
                for (int q = 0; q <= posts; q++)
                    SacredDraw.Box(m, x, -half + 2 * half * q / posts - 0.05, -half + 2 * half * q / posts + 0.05, hb, hb + 2.5, 1.05, 1.15, PashupatiKit.WoodDark,
                                   BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            SacredDraw.Quad(m, x, -half - 0.2, hb + 2.6, 0, half + 0.2, hb + 2.6, 0, half + 0.2, hb + 2.2, 1.5, -half - 0.2, hb + 2.2, 1.5, 0, 1, 0.27,
                            PashupatiKit.RustRoof);
            SacredDraw.Quad(m, x, -half - 0.2, hb + 2.2, 1.5, half + 0.2, hb + 2.2, 1.5, half + 0.2, hb + 2.05, 1.5, -half - 0.2, hb + 2.05, 1.5, 0, 0, 1, ParapetRed);
        }

        /// <summary>PT-08, the Mukti Mandap (w913217412) on its white riverside block: the raised floor, the dark carved
        /// wooden arcade (five cusped arches a side on carved posts, a frieze), the single wide grey metal hip roof with deep
        /// eaves and its white plaster mini-shikhara finial with a gilt spike; the block's river face (white, a red band)
        /// with the red-and-white crenellated parapet pilgrims lean on, and the red pyramidal shrine between the pavilion
        /// and the East Gate.</summary>
        public static void MuktiMandap(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod, v0 = m.VertexCount, i0 = m.IndexCount;
            const double A0 = 18.6, A1 = 28.0, B0 = -10.5, B1 = -1.1, Floor = 0.6, Post = 3.6, Frieze = 4.5, RoofRise = 4.1;
            double ca = 0.5 * (A0 + A1), cb = 0.5 * (B0 + B1), hu = 0.5 * (A1 - A0), hw = 0.5 * (B1 - B0);
            Affine3 k = f.At(ca, 0, cb);
            double foot = Math.Min(-2.5, f.GroundAt(30.5, -5.0, -4.2));
            // The riverside block's face: from the ghat walk to the courtyard level along a = +28.4, b −11 … +4.9.
            RiverWall(ref f, m, 28.4, -11.0, 28.4, 4.9, foot, lod);
            if (lod >= 3)
            {
                PashupatiKit.Block(m, k, -hu, hu, 0, Frieze, -hw, hw, PashupatiKit.WoodDark);
                Shapes.Cone(m, SacredDraw.At(k, 0, Frieze, 0), MuktiGrey, 7.0, RoofRise, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            // Raised floor.
            PashupatiKit.Band(m, k, 0, 0, hu, hw, new[] { 0.06, -0.2, 0.06, Floor - 0.12, 0.12, Floor - 0.06, 0.12, Floor }, PashupatiKit.StoneEdge, true,
                              PashupatiKit.Br(0x9A958A, MaterialChannel.Flagstone));
            // The arcade: carved posts round the floor (five bays a side), the cusped arches between them, the frieze.
            int bays = 5;
            for (int s = 0; s < 4; s++)
            {
                double du, dw;
                PashupatiKit.Out(s, out du, out dw);
                double off = s % 2 == 0 ? hw : hu, half = s % 2 == 0 ? hu : hw;
                Affine3 x = SacredDraw.Basis(k, du * (off - 0.15), 0, dw * (off - 0.15), dw, 0, -du, 0, 1, 0);
                for (int q = 0; q <= bays; q++)
                {
                    double u = -half + 2 * half * q / bays;
                    SacredParts.Post(m, x, u, 0, Floor, Post, 0.26, PashupatiKit.WoodDark, lod);
                }
                if (lod <= 1)
                    for (int q = 0; q < bays; q++)
                    {
                        double u = -half + 2 * half * (q + 0.5) / bays, span = 2 * half / bays - 0.3;
                        ShikharaGenerator.Arch(m, x, u, Post - 0.35 * span - 0.1, 0.0, span, 0.35 * span, 0.08, 0.06, true, PashupatiKit.WoodDark, lod + 1);
                    }
                SacredDraw.Box(m, x, -half - 0.2, half + 0.2, Post, Frieze, -0.1, 0.16, PashupatiKit.WoodDark, BoxFaces.Front | BoxFaces.Bottom);
                if (lod == 0)
                    for (int q = 0; q < 2 * bays; q++)
                        Shapes.Sphere(m, SacredDraw.At(x, -half + 2 * half * (q + 0.5) / (2 * bays), Post + 0.45, 0.18), PashupatiKit.Br(0x4A3628, MaterialChannel.WoodCarved),
                                      0.09, 5);
            }
            // Ceiling and the dark inner core (the floor under the roof is open; people sit on mats).
            SacredDraw.Box(m, k, -hu, hu, Frieze - 0.02, Frieze, -hw, hw, PashupatiKit.Ao(PashupatiKit.WoodDark, 0.6f), BoxFaces.Bottom);
            if (lod <= 1)
                for (int q = 0; q < 3; q++)
                    SacredDraw.Box(m, k, -2.2 + 2.2 * q - 0.6, -2.2 + 2.2 * q + 0.6, Floor + 0.01, Floor + 0.03, -1.5, 1.5,
                                   PashupatiKit.Br(q == 1 ? 0xC8A33Au : 0x2E7D4Au, MaterialChannel.Fabric), BoxFaces.Top);
            // The grey metal hip roof with deep eaves, its soffit, and the white mini-shikhara finial with its gilt spike.
            var r = new PRoof
            {
                EaveU = hu + 1.3, EaveW = hw + 1.3, EaveV = Frieze - 0.05, TopU = 0.55, TopW = 0.55, TopV = Frieze + RoofRise, Lift = 0.12, FasciaH = 0.2,
                SeamPitch = 0.62, CurlSize = 0, Roof = MuktiGrey, Seam = MuktiRib, Fascia = PashupatiKit.Br(0x6E767C, MaterialChannel.Metal), Curl = MuktiRib,
            };
            PashupatiKit.Roof(m, k, r, lod);
            PashupatiKit.Skirt(m, k, hu + 0.1, hw + 0.1, Frieze, hu + 1.25, hw + 1.25, Frieze - 0.25, 1, 2);
            double top = Frieze + RoofRise;
            PashupatiKit.Block(m, k, -0.6, 0.6, top, top + 0.15, -0.6, 0.6, BandWhite);
            PashupatiKit.LatheOf(m, SacredDraw.At(k, 0, top + 0.15, 0), BandWhite,
                                 new[] { 0.0, 0.0, 0.45, 0.0, 0.45, 0.12, 0.36, 0.2, 0.34, 0.6, 0.28, 0.75, 0.16, 0.9, 0.18, 0.95, 0.0, 0.98 }, 8, lod, 0b1_0000_0110);
            PashupatiKit.BellFinial(m, k, 0, top + 1.1, 0, MuktiTopM - top - 1.1, 0.12, PashupatiKit.GiltBright, lod);
            // The red pyramidal shrine between the pavilion and the East Gate (c_gje/c_03, c_04).
            PashupatiShrines.RedShikhara(ref f, m, 20.8, 2.2, 4.3);
            SacredDraw.Bake(m, v0, i0, f.Level);
            // Colliders: the raised floor (walkable), the posts.
            f.Box(A0, A1, B0, B1, -0.3, Floor, GenColliderFlags.Walkable, GenColliders.Stone);
            for (int q = 0; q <= bays; q++)
            {
                double t = (double)q / bays;
                f.Cylinder(A0 + 0.15 + t * (A1 - A0 - 0.3), B0 + 0.15, Floor, Post, 0.15);
                f.Cylinder(A0 + 0.15 + t * (A1 - A0 - 0.3), B1 - 0.15, Floor, Post, 0.15);
            }
        }

        /// <summary>PT-21, the south-east riverside block (w913217411): white plaster, two storeys over the courtyard and
        /// a flat roof, its river face dropping to the ghat walk with the same red-and-white parapet.</summary>
        public static void SouthEastBlock(ref PashupatiFrame f, MeshData m)
        {
            double foot = Math.Min(-2.5, f.GroundAt(34.0, -19.0, -4.2));
            House h = Plastered(SouthEast, 0, 2, 3.1, WhitePlaster, WinStyle.Arched, RoofKind.Flat, default, default, 91u);
            h.Bottom = foot - 0.3;
            h.Frame = WindowRed;
            Build(ref f, m, h);
            if (f.Lod <= 1) Crenellation(ref f, m, 32.7, -25.5, 32.1, -13.1, 6.2 + h.Parapet, f.Lod);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Pieces
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The white riverside retaining wall from the ghat walk up to the courtyard along (a0, b0) → (a1, b1),
        /// with a red band, and the red-and-white crenellated parapet on top (1.1 m: cream panels between red piers with red
        /// caps) where pilgrims lean to watch the river.</summary>
        private static void RiverWall(ref PashupatiFrame f, MeshData m, double a0, double b0, double a1, double b1, double foot, int lod)
        {
            double ea = a1 - a0, eb = b1 - b0, el = Math.Sqrt(ea * ea + eb * eb), na = -eb / el, nb = ea / el;
            // Outward normal toward the river (+a).
            if (na < 0)
            {
                na = -na;
                nb = -nb;
            }
            Affine3 x = f.Facing(0.5 * (a0 + a1), 0, 0.5 * (b0 + b1), na, nb);
            double half = 0.5 * el;
            SacredDraw.Box(m, x, -half, half, foot - 0.3, 0, -0.6, 0, WhitePlaster, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Box(m, x, -half, half, -0.9, -0.55, 0, 0.03, ParapetRed, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            Crenellation(ref f, m, a0, b0, a1, b1, 0, lod);
            f.Wall(a0, b0, a1, b1, foot, 1.1, 0.3);
        }

        /// <summary>The red-and-white crenellated parapet along a line at height y: cream panels between red piers with red
        /// caps and a red base band, 1.1 m.</summary>
        internal static void Crenellation(ref PashupatiFrame f, MeshData m, double a0, double b0, double a1, double b1, double y, int lod)
        {
            double ea = a1 - a0, eb = b1 - b0, el = Math.Sqrt(ea * ea + eb * eb);
            double da = ea / el, db = eb / el;
            Affine3 x = f.Facing(0.5 * (a0 + a1), y, 0.5 * (b0 + b1), db, -da);
            double half = 0.5 * el;
            SacredDraw.Box(m, x, -half, half, 0, 0.18, -0.15, 0.15, ParapetRed, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, x, -half, half, 0.18, 0.95, -0.1, 0.1, ParapetCream, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod >= 2) return;
            int piers = Math.Max(2, (int)Math.Round(el / 0.9));
            for (int q = 0; q <= piers; q++)
            {
                double u = -half + el * q / piers;
                SacredDraw.Box(m, x, u - 0.1, u + 0.1, 0.18, 1.0, -0.14, 0.14, ParapetRed, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x, u - 0.14, u + 0.14, 1.0, 1.1, -0.17, 0.17, ParapetRed, BoxFaces.All & ~BoxFaces.Bottom);
                if (lod == 0 && q < piers)
                    SacredDraw.Box(m, x, u + 0.1, u + el / piers - 0.1, 0.18, 0.3, -0.18, 0.18, ParapetRed, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            }
        }

        /// <summary>The gilt door between two painted eyes on the south flank's forecourt face, under its red LED board
        /// ("a golden door sits just to the right of the western entrance gate").</summary>
        private static void GoldenDoor(ref PashupatiFrame f, MeshData m)
        {
            if (f.Lod >= 2) return;
            double fr = PashupatiSite.ForecourtRiseM;
            // On the golden-door house's forecourt face, the edge from (−46.03, −8.96) to (−45.51, −16.0), at its middle.
            double ea = -45.51 - -46.03, eb = -16.0 - -8.96, el = Math.Sqrt(ea * ea + eb * eb), na = eb / el, nb = -ea / el;
            double ma = -45.77 + 0.02 * na, mb = -12.5 + 0.02 * nb;
            Affine3 x = f.Facing(ma, fr, mb, na, nb);
            // The door (c_westgate/c_03): a black surround with a peaked top, the thick gilt frame rising to an ogee point
            // over the arch, and the two dark bronze leaves with raised panels and gilt bosses.
            ShapeBrush black = PashupatiKit.Br(0x1E1B19, MaterialChannel.WoodCarved), brass = PashupatiKit.Br(0x3E3222, MaterialChannel.Metal);
            SacredDraw.Box(m, x, -0.98, 0.98, 0, 2.75, 0, 0.1, black, BoxFaces.Wall);
            SacredDraw.Tri(m, x, -0.98, 2.75, 0.1, 0.98, 2.75, 0.1, 0, 3.25, 0.1, 0, 0, 1, black);
            SacredDraw.Panel(m, x, -0.62, 0.02, 0.62, 2.25, 0.12, brass);
            if (f.Lod == 0)
            {
                for (int s = -1; s <= 1; s += 2)
                    for (int r = 0; r < 3; r++)
                    {
                        double u0 = s * 0.08, u1 = s * 0.54, v0 = 0.2 + 0.68 * r, v1 = v0 + 0.56;
                        SacredDraw.Box(m, x, Math.Min(u0, u1), Math.Max(u0, u1), v0, v1, 0.12, 0.15, PashupatiKit.Br(0x5E4C2C, MaterialChannel.Metal), BoxFaces.Front | BoxFaces.Wall);
                        Shapes.Sphere(m, SacredDraw.At(x, s * 0.31, v0 + 0.28, 0.17), PashupatiKit.GiltBright, 0.05, 5);
                    }
            }
            Path3 gf = PashupatiKit.PathS;
            gf.Add(-0.72, 0.0, 0.15).Add(-0.72, 2.25, 0.15);
            for (int i = 1; i < 8; i++)
            {
                double t = i / 8.0, u = -0.72 * Math.Cos(0.5 * Math.PI * t), v = 2.25 + 0.42 * Math.Sin(0.5 * Math.PI * t) + 0.18 * t * t;
                gf.Add(u, v, 0.15);
            }
            gf.Add(0, 2.9, 0.15);
            for (int i = 7; i >= 1; i--)
            {
                double t = i / 8.0, u = 0.72 * Math.Cos(0.5 * Math.PI * t), v = 2.25 + 0.42 * Math.Sin(0.5 * Math.PI * t) + 0.18 * t * t;
                gf.Add(u, v, 0.15);
            }
            gf.Add(0.72, 2.25, 0.15).Add(0.72, 0.0, 0.15);
            Shapes.Tube(m, x, PashupatiKit.GiltBright, gf, 0.08, f.Lod == 0 ? 6 : 4, true);
            Shapes.Sphere(m, SacredDraw.At(x, 0, 2.98, 0.15), PashupatiKit.GiltBright, 0.08, 5);
            for (int s = -1; s <= 1; s += 2)
            {
                // The painted eyes (white almond, dark pupil, black brow) either side.
                Shapes.Ellipsoid(m, SacredDraw.At(x, s * 1.45, 2.0, 0.02), PashupatiKit.Br(0xF5F2EA, MaterialChannel.Paint), 0.42, 0.17, 0.02, 8, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, s * 1.45, 2.0, 0.04), PashupatiKit.Ink, 0.13, 0.13, 0.02, 8, false);
                Shapes.Ellipsoid(m, SacredDraw.At(x, s * 1.45, 2.3, 0.03), PashupatiKit.Ink, 0.45, 0.05, 0.02, 6, false);
            }
            SacredDraw.Box(m, x, -1.9, 1.9, 3.1, 3.55, 0, 0.12, PashupatiKit.Br(0x24201E, MaterialChannel.Plain), BoxFaces.Wall);
            SacredDraw.Box(m, x, -1.8, 1.8, 3.2, 3.45, 0.12, 0.13, PashupatiKit.Br(0xE0302A, MaterialChannel.Plain), BoxFaces.Front);
            // The gabled dormer on the rust roof over the door (c_westgate/c_03): a white cheek box with a small window
            // and its own rust gable roof, set back up the slope.
            double eave = 2 * 3.3;
            Affine3 d = SacredDraw.At(x, 0, eave - 0.1, -1.9);
            SacredDraw.Box(m, d, -0.8, 0.8, 0, 1.35, -1.4, 0.0, WhitePlaster, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Panel(m, d, -0.4, 0.3, 0.4, 1.05, 0.012, Glass);
            SacredDraw.Box(m, d, -0.48, 0.48, 0.22, 0.3, 0, 0.08, PashupatiKit.WoodDark, BoxFaces.Wall | BoxFaces.Top);
            for (int s = -1; s <= 1; s += 2)
                SacredDraw.Quad(m, d, s * 1.0, 1.25, 0.25, 0, 2.0, 0.25, 0, 2.0, -1.6, s * 1.0, 1.25, -1.6, s * 0.6, 0.8, 0, PashupatiKit.RustRoof);
            SacredDraw.Tri(m, d, -0.8, 1.35, 0.01, 0.8, 1.35, 0.01, 0, 1.95, 0.01, 0, 0, 1, WhitePlaster);
        }

        // ---------------------------------------------------------------------------------------------------------
        // The house builder
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A plastered house on an outline (site metres), standing on the given floor level.</summary>
        private static House Plastered(double[] plan, double floor, int storeys, double storeyM, in ShapeBrush wall, WinStyle win, RoofKind roof, in ShapeBrush roofB,
                                       in ShapeBrush ribB, uint seed)
        {
            return new House
            {
                Plan = plan, Bottom = floor - 0.4, Floor = floor, Storeys = storeys, StoreyM = storeyM, Wall = wall, Band = BandWhite, Frame = WindowWood,
                Windows = win, WindowPitch = 2.6, Roof = roof, RoofB = roofB, RibB = ribB, Overhang = 0.7, Pitch = 0.42, Parapet = 0.8, MinWall = 2.2, Seed = seed,
            };
        }

        /// <summary>The lowest terrain under an outline (relative to the courtyard), for houses off the level zones.</summary>
        private static double Ground(ref PashupatiFrame f, double[] plan)
        {
            double g = double.MaxValue, ca = 0, cb = 0;
            int n = plan.Length / 2;
            for (int i = 0; i < n; i++)
            {
                ca += plan[2 * i] / n;
                cb += plan[2 * i + 1] / n;
            }
            for (int i = 0; i < n; i++) g = Math.Min(g, f.GroundAt(plan[2 * i], plan[2 * i + 1], 0));
            g = Math.Min(g, f.GroundAt(ca, cb, 0));
            return Math.Max(-1.5, Math.Min(2.5, g));
        }

        /// <summary>
        /// A house: walls swept round the outline (plinth course, an optional dado, a band at every floor, the parapet of a
        /// flat roof), windows on every wall long enough that is not a party wall (lattice, arched or plain; skipped below
        /// the ground), and the roof: a terrace slab inside the parapet, or a hipped or gabled corrugated-metal roof with
        /// standing seams over the outline's oriented box. LOD1 draws windows as dark panes, LOD2 walls and roof, LOD3 a box and a pyramid. Walls
        /// along every edge are solid (never walked into).
        /// </summary>
        internal static void Build(ref PashupatiFrame f, MeshData m, in House h)
        {
            int lod = f.Lod, v0 = m.VertexCount, i0 = m.IndexCount;
            if (_pa == null)
            {
                _pa = new double[64];
                _pb = new double[64];
            }
            int n = Math.Min(h.Plan.Length / 2, 64);
            for (int i = 0; i < n; i++)
            {
                _pa[i] = h.Plan[2 * i];
                _pb[i] = h.Plan[2 * i + 1];
            }
            if (SacredDraw.Area(_pa, _pb, n) < 0)
            {
                Array.Reverse(_pa, 0, n);
                Array.Reverse(_pb, 0, n);
            }
            double top = h.Floor + h.Storeys * h.StoreyM;
            double ca, cb, ux, uz, hu, hw;
            Obb(_pa, _pb, n, out ca, out cb, out ux, out uz, out hu, out hw);
            Affine3 box = Affine3.FromBasis(ux, 0, uz, 0, 1, 0, -uz, 0, ux, ca, 0, cb).Then(f.Site);
            if (lod >= 3)
            {
                PashupatiKit.Block(m, box, -hu, hu, h.Bottom, top, -hw, hw, h.Wall);
                if (h.Roof != RoofKind.Flat) Shapes.Cone(m, SacredDraw.At(box, 0, top, 0), h.RoofB, Math.Max(hu, hw) * 1.2, h.Pitch * hw + 0.3, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level + h.Floor);
                return;
            }
            // Walls.
            Mould p = SacredDraw.M;
            p.Add(0.06, h.Bottom, PlinthStone).Add(0.06, h.Floor + 0.2);
            if (h.DadoM > 0) p.Add(0, h.Floor + 0.24, h.Dado).Add(0, h.Floor + h.DadoM, h.Wall);
            else p.Add(0, h.Floor + 0.24, h.Wall);
            for (int s = 1; s < h.Storeys && lod <= 1; s++)
            {
                double y = h.Floor + s * h.StoreyM;
                p.Add(0, y - 0.1, h.Band).Add(0.07, y - 0.06).Add(0.07, y + 0.04).Add(0, y + 0.08, h.Wall);
            }
            if (h.Roof == RoofKind.Flat)
            {
                p.Add(0, top - 0.1, h.Band).Add(0.1, top - 0.05).Add(0.1, top + h.Parapet);
            }
            else
            {
                p.Add(0, top, h.Wall);
            }
            SacredDraw.Ring(m, f.Site, _pa, _pb, n, p);
            // Windows on each wall long enough.
            if (lod <= 1)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double ea = _pa[j] - _pa[i], eb = _pb[j] - _pb[i], el = Math.Sqrt(ea * ea + eb * eb);
                    if (el < h.MinWall) continue;
                    double na = eb / el, nb = -ea / el, ma = 0.5 * (_pa[i] + _pa[j]), mb = 0.5 * (_pb[i] + _pb[j]);
                    // A party wall (another building right outside it) has no windows.
                    if (PartyWall(ref f, h.Plan, ma + 1.2 * na, mb + 1.2 * nb)) continue;
                    double ground = f.GroundAt(ma + 1.5 * na, mb + 1.5 * nb, h.Floor);
                    Affine3 x = f.Facing(ma, 0, mb, na, nb);
                    int k = Math.Max(1, (int)Math.Floor((el - 1.0) / h.WindowPitch));
                    for (int s = 0; s < h.Storeys; s++)
                    {
                        double sill = h.Floor + s * h.StoreyM + (s == 0 ? 0.9 : 0.75);
                        if (sill < ground + 0.3 || (s == 0 && h.BlindGround)) continue;
                        double wh = Math.Min(1.6, h.StoreyM - 1.3), ww = h.Windows == WinStyle.Arched ? 0.85 : 1.0;
                        for (int q = 0; q < k; q++)
                        {
                            double u = -0.5 * el + el * (q + 0.5) / k;
                            Window(m, x, u, sill, ww, wh, h, lod, (uint)(i * 31 + s * 7 + q) ^ h.Seed);
                        }
                    }
                }
            }
            // Roof.
            if (h.Roof == RoofKind.Flat)
            {
                Shapes.BevelExtrude(m, f.At(0, top + h.Parapet - 0.2, 0), PashupatiKit.Br(0xCFCBC2, MaterialChannel.Concrete), _pa, _pb, n, 0.04, 0, 0, BevelStyle.Chamfer,
                                    true, false);
            }
            else
            {
                double eu = hu + h.Overhang, ew = hw + h.Overhang, rise = h.Pitch * ew;
                Affine3 rk = SacredDraw.At(box, 0, top, 0);
                var r = new PRoof
                {
                    EaveU = eu, EaveW = ew, EaveV = -0.25, TopU = h.Roof == RoofKind.Gable ? eu : Math.Max(0.15, eu - ew), TopW = 0.12, TopV = rise, Lift = 0.0,
                    FasciaH = 0.14, SeamPitch = lod == 0 ? 1.0 : 0, CurlSize = 0, Roof = h.RoofB, Seam = h.RibB, Fascia = PashupatiKit.Ao(h.RoofB, 0.8f), Curl = h.RibB,
                };
                PashupatiKit.Roof(m, rk, r, lod);
                PashupatiKit.Skirt(m, rk, hu, hw, 0, eu - 0.05, ew - 0.05, -0.39, 1, 2);
                if (h.Roof == RoofKind.Gable)
                {
                    // Gable ends in the wall's plaster.
                    for (int s = -1; s <= 1; s += 2)
                        SacredDraw.Tri(m, rk, s * hu, 0, -hw, s * hu, 0, hw, s * hu, rise * hw / ew, 0, s, 0, 0, h.Wall);
                }
            }
            SacredDraw.Bake(m, v0, i0, f.Level + h.Floor);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                f.WallClear(_pa[i], _pb[i], _pa[j], _pb[j], h.Bottom, top, 0.25);
            }
        }

        /// <summary>Every outline the ring builds (site metres), for the party-wall test.</summary>
        private static readonly double[][] Plans =
        {
            NorthFlank, NorthLink, SouthFlank, InnerSouth, InnerNorth, GoldenDoorHouse, SouthWest, SouthWest2, North, NorthHouseW, NorthHouseN, NorthHouseE,
            South, Mulbhatt, Priests2, Priests3, SouthEast, EastWingPlan,
        };

        /// <summary>
        /// Whether site (a, b), just outside a wall of <paramref name="own"/>, lies inside another building: one of the
        /// ring's outlines or a compact OSM building of the tile (at most 60 m across, so no compound outline counts). Such
        /// a wall is a party wall, and the houses draw no windows on it.
        /// </summary>
        private static bool PartyWall(ref PashupatiFrame f, double[] own, double a, double b)
        {
            for (int i = 0; i < Plans.Length; i++)
                if (!ReferenceEquals(Plans[i], own) && InPlan(Plans[i], a, b)) return true;
            if (f.Tile == null) return false;
            double x, z;
            f.ToTile(a, b, out x, out z);
            x *= 100;
            z *= 100;
            var list = f.Tile.Buildings;
            for (int i = 0; i < list.Count; i++)
            {
                int[] r = list[i].Rings[0];
                int n = r.Length / 2, x0 = int.MaxValue, x1 = int.MinValue, z0 = int.MaxValue, z1 = int.MinValue;
                for (int k = 0; k < n; k++)
                {
                    x0 = Math.Min(x0, r[2 * k]);
                    x1 = Math.Max(x1, r[2 * k]);
                    z0 = Math.Min(z0, r[2 * k + 1]);
                    z1 = Math.Max(z1, r[2 * k + 1]);
                }
                if (x < x0 || x > x1 || z < z0 || z > z1 || x1 - x0 > 6000 || z1 - z0 > 6000) continue;
                bool inside = false;
                for (int k = 0, j = n - 1; k < n; j = k++)
                {
                    double xk = r[2 * k], zk = r[2 * k + 1], xj = r[2 * j], zj = r[2 * j + 1];
                    if ((zk > z) != (zj > z) && x < (xj - xk) * (z - zk) / (zj - zk) + xk) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        /// <summary>Point in an outline given as (a, b) pairs.</summary>
        private static bool InPlan(double[] plan, double a, double b)
        {
            int n = plan.Length / 2;
            bool inside = false;
            for (int k = 0, j = n - 1; k < n; j = k++)
            {
                double ak = plan[2 * k], bk = plan[2 * k + 1], aj = plan[2 * j], bj = plan[2 * j + 1];
                if ((bk > b) != (bj > b) && a < (aj - ak) * (b - bk) / (bj - bk) + ak) inside = !inside;
            }
            return inside;
        }

        /// <summary>One window centred at u from its sill on face frame x.</summary>
        private static void Window(MeshData m, in Affine3 x, double u, double sill, double ww, double wh, in House h, int lod, uint seed)
        {
            double hu = 0.5 * ww;
            if (lod >= 1)
            {
                SacredDraw.Panel(m, x, u - hu, sill, u + hu, sill + wh, 0.012, Glass);
                return;
            }
            switch (h.Windows)
            {
                case WinStyle.Lattice:
                    LatticeLight(m, x, u, sill, ww, wh, h.Frame);
                    break;
                case WinStyle.Arched:
                {
                    double r = hu, body = wh - r;
                    SacredDraw.Panel(m, x, u - hu, sill, u + hu, sill + body, 0.012, Glass);
                    int c = SacredDraw.V(m, x, u, sill + body, 0.012, 0, 0, 1, Glass), first = m.VertexCount;
                    const int Arc = 4;
                    for (int i = 0; i <= Arc; i++)
                    {
                        double a = Math.PI * i / Arc;
                        SacredDraw.V(m, x, u + r * Math.Cos(a), sill + body + r * Math.Sin(a), 0.012, 0, 0, 1, Glass);
                    }
                    for (int i = 0; i < Arc; i++) SacredDraw.Tri(m, c, first + i, first + i + 1);
                    Path3 p = PashupatiKit.PathS;
                    p.Add(u - hu - 0.06, sill, 0.04).Add(u - hu - 0.06, sill + body, 0.04);
                    for (int i = 1; i < Arc; i++)
                    {
                        double a = Math.PI - Math.PI * i / Arc;
                        p.Add(u + (r + 0.06) * Math.Cos(a), sill + body + (r + 0.06) * Math.Sin(a), 0.04);
                    }
                    p.Add(u + hu + 0.06, sill + body, 0.04).Add(u + hu + 0.06, sill, 0.04);
                    Shapes.Tube(m, x, h.Frame, p, 0.055, 3, false);
                    SacredDraw.Box(m, x, u - hu - 0.12, u + hu + 0.12, sill - 0.1, sill, 0, 0.12, h.Frame, BoxFaces.Wall);
                    // Lattice shutters half closed in some windows.
                    if ((seed & 3u) == 1u)
                        SacredDraw.Panel(m, x, u - hu, sill, u, sill + body, 0.03, PashupatiKit.Br(0x6E2A22, MaterialChannel.Wood));
                    break;
                }
                default:
                    SacredDraw.Panel(m, x, u - hu, sill, u + hu, sill + wh, 0.012, Glass);
                    SacredDraw.Box(m, x, u - hu - 0.1, u + hu + 0.1, sill - 0.08, sill, 0, 0.1, h.Band, BoxFaces.Wall);
                    SacredDraw.Box(m, x, u - hu - 0.06, u - hu, sill, sill + wh, 0, 0.05, h.Frame, BoxFaces.Front | BoxFaces.Left);
                    SacredDraw.Box(m, x, u + hu, u + hu + 0.06, sill, sill + wh, 0, 0.05, h.Frame, BoxFaces.Front | BoxFaces.Right);
                    SacredDraw.Box(m, x, u - hu - 0.06, u + hu + 0.06, sill + wh, sill + wh + 0.06, 0, 0.05, h.Frame, BoxFaces.Front | BoxFaces.Top);
                    break;
            }
        }

        /// <summary>A house's lattice window (about 20 triangles, the ring has hundreds): the dark recess, a flat frame proud
        /// of the wall with a projecting sill and lintel, and two mullions of the lattice.</summary>
        private static void LatticeLight(MeshData m, in Affine3 x, double u, double sill, double ww, double wh, in ShapeBrush frame)
        {
            double hu = 0.5 * ww, fw = 0.08, top = sill + wh;
            SacredDraw.Panel(m, x, u - hu, sill, u + hu, top, 0.01, Looks.Recess);
            SacredDraw.Panel(m, x, u - hu - fw, sill, u - hu, top, 0.05, frame);
            SacredDraw.Panel(m, x, u + hu, sill, u + hu + fw, top, 0.05, frame);
            SacredDraw.Box(m, x, u - hu - fw - 0.1, u + hu + fw + 0.1, top, top + fw, 0, 0.09, frame, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top);
            SacredDraw.Box(m, x, u - hu - fw - 0.1, u + hu + fw + 0.1, sill - fw, sill, 0, 0.09, frame, BoxFaces.Front | BoxFaces.Top);
            for (int i = 1; i <= 2; i++)
                SacredDraw.Panel(m, x, u - hu + ww * i / 3 - 0.025, sill, u - hu + ww * i / 3 + 0.025, top, 0.035, frame);
        }

        /// <summary>The oriented bounding box of an outline along its longest edge: centre, unit axis (along), half sizes.</summary>
        private static void Obb(double[] pa, double[] pb, int n, out double ca, out double cb, out double ux, out double uz, out double hu, out double hw)
        {
            double best = -1;
            ux = 1;
            uz = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double da = pa[j] - pa[i], db = pb[j] - pb[i], l = Math.Sqrt(da * da + db * db);
                if (l <= best) continue;
                best = l;
                ux = da / l;
                uz = db / l;
            }
            double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double u = pa[i] * ux + pb[i] * uz, w = -pa[i] * uz + pb[i] * ux;
                u0 = Math.Min(u0, u);
                u1 = Math.Max(u1, u);
                w0 = Math.Min(w0, w);
                w1 = Math.Max(w1, w);
            }
            double cu = 0.5 * (u0 + u1), cw = 0.5 * (w0 + w1);
            ca = cu * ux - cw * uz;
            cb = cu * uz + cw * ux;
            hu = 0.5 * (u1 - u0);
            hw = 0.5 * (w1 - w0);
        }
    }
}
