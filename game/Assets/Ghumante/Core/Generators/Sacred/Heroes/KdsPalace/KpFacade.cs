using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>What the ground floor of a white range shows.</summary>
    internal enum KpFoot : byte
    {
        Plain = 0,

        /// <summary>A continuous row of blind cusped arches (the Dakha and gate fronts).</summary>
        BlindArches = 1,

        /// <summary>Small dark lattice windows in white surrounds (the Basantapur wings).</summary>
        Lattice = 2,

        /// <summary>An arcade of cream-yellow columns and arches (Nasal Chowk's north range).</summary>
        Arcade = 3,
    }

    /// <summary>
    /// The facade languages of the palace (kds-palace.md §5.1): the Rana white ranges (plaster, dado, blind arches or
    /// lattice windows at the foot, rows of shuttered windows, a black timber gallery under the eave) and the Newar
    /// brick ranges (string courses of carved timber, rows of lattice windows, a projecting three-bay window at the centre,
    /// a carved lattice band, a ground-floor colonnade). Each dresses one face frame from u = −half to +half.
    /// </summary>
    internal static class KpFacade
    {
        /// <summary>
        /// A white Rana face: ground floor <paramref name="ground"/> from v0 to f1, upper floors at the levels in
        /// <paramref name="floors"/> (each floor's sill line is its level + 0.6), windows every <paramref name="pitch"/>
        /// metres with the given shutters, an optional black gallery on the top floor and a red dado.
        /// </summary>
        public static void Rana(MeshData m, in Affine3 k, double half, double v0, double[] floors, int nFloors, double eave, KpFoot ground, double pitch,
                                in ShapeBrush shutter, bool closed, bool gallery, bool dado, int lod)
        {
            double f1 = nFloors > 0 ? floors[0] : eave;
            double margin = Math.Min(1.2, 0.18 * half);
            if (dado)
            {
                Kp.Band(m, k, -half, half, v0, v0 + 0.55, 0.05, Kp.Dado);
                if (lod <= 1) Kp.Box(m, k, -half + 0.3, half - 0.3, v0, v0 + 0.42, 0, 0.38, Kp.Dado, BoxFaces.Wall);
            }
            double g0 = v0 + (dado ? 0.65 : 0.25);
            int bays = Math.Max(1, (int)Math.Round((2 * half - 2 * margin) / pitch));
            double bay = (2 * half - 2 * margin) / bays;
            switch (ground)
            {
                case KpFoot.BlindArches:
                {
                    int n = Math.Max(1, (int)Math.Round(2 * half / 2.35));
                    double w = 2 * half / n;
                    for (int i = 0; i < n; i++) Kp.BlindArch(m, k, -half + w * (i + 0.5), g0, w - 0.45, Math.Min(2.8, f1 - g0 - 0.5), lod);
                    break;
                }
                case KpFoot.Lattice:
                    for (int i = 0; i < bays; i++)
                    {
                        double u = -half + margin + bay * (i + 0.5);
                        double h = Math.Min(1.3, f1 - g0 - 1.0);
                        if (i % 3 == 1 && lod <= 1) Kp.Doorway(m, k, u, v0, 1.1, Math.Min(2.4, f1 - v0 - 0.5), Kp.Wood, false, lod);
                        else
                        {
                            if (lod <= 1) Kp.Box(m, k, u - 0.62, u + 0.62, g0 + 0.25, g0 + 0.45 + h, 0, 0.05, Kp.WhiteTrim, BoxFaces.Wall);
                            Kp.Lattice(m, k, u, g0 + 0.35 + 0.5 * h, 0.85, h, lod, 3);
                        }
                    }
                    break;
            }
            // String courses at each floor and the upper windows.
            for (int fl = 0; fl < nFloors; fl++)
            {
                double lv = floors[fl], next = fl + 1 < nFloors ? floors[fl + 1] : eave;
                Kp.Band(m, k, -half, half, lv - 0.12, lv + 0.08, 0.09, Kp.WhiteTrim);
                bool top = fl == nFloors - 1;
                if (top && gallery)
                {
                    Kp.Gallery(m, k, -half - 0.05, half + 0.05, lv + 0.3, eave - lv - 0.75, 0.95, 0.95, lod);
                    // The windows behind the gallery.
                    for (int i = 0; i < bays; i++)
                        Kp.RanaWindow(m, k, -half + margin + bay * (i + 0.5), lv + 0.75, Math.Min(0.95, 0.5 * bay), Math.Min(1.6, eave - lv - 1.8), shutter, closed, Math.Max(1, lod));
                    continue;
                }
                double wh = Math.Min(1.9, next - lv - 1.2);
                if (wh < 0.6) continue;
                for (int i = 0; i < bays; i++)
                {
                    double u = -half + margin + bay * (i + 0.5);
                    Kp.RanaWindow(m, k, u, lv + 0.65, Math.Min(1.05, 0.55 * bay), wh, shutter, closed, lod);
                }
            }
            // A plain moulded cornice under the eave.
            Kp.Band(m, k, -half - 0.05, half + 0.05, eave - 0.45, eave - 0.25, 0.12, Kp.WhiteTrim);
        }

        /// <summary>
        /// A Newar brick face: carved timber string courses at the floor levels, rows of lattice windows, an optional
        /// projecting three-bay window at the centre of floor <paramref name="bayFloor"/>, an optional continuous carved
        /// lattice band on floor <paramref name="bandFloor"/>, and an optional ground-floor colonnade of carved posts.
        /// </summary>
        public static void Newar(MeshData m, in Affine3 k, double half, double v0, double[] floors, int nFloors, double eave, double pitch, int bayFloor, int bandFloor,
                                 bool colonnade, bool door, int lod)
        {
            double margin = Math.Min(1.0, 0.15 * half);
            int bays = Math.Max(1, (int)Math.Round((2 * half - 2 * margin) / pitch));
            double bay = (2 * half - 2 * margin) / bays;
            double f1 = nFloors > 0 ? floors[0] : eave;
            // Plinth course.
            Kp.Band(m, k, -half, half, v0, v0 + 0.35, 0.07, Kp.Step);
            if (colonnade)
            {
                // An open colonnade: the dark depth behind and carved posts on stone bases.
                Kp.Panel(m, k, -half + 0.3, v0 + 0.35, half - 0.3, f1 - 0.2, 0.01, Kp.Ao(Kp.Recess, 0.85f));
                int n = Math.Max(2, (int)Math.Round(2 * half / 2.4));
                for (int i = 0; i <= n; i++)
                {
                    double u = -half + 0.3 + (2 * half - 0.6) * i / n;
                    if (lod >= 2) Kp.Box(m, k, u - 0.12, u + 0.12, v0 + 0.35, f1 - 0.2, 0.02, 0.26, Kp.Wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    else SacredParts.Post(m, SacredDraw.At(k, 0, 0, 0.16), u, 0, v0 + 0.35, f1 - 0.32, 0.24, Kp.Wood, lod);
                }
                Kp.Band(m, k, -half, half, f1 - 0.32, f1 - 0.1, 0.3, Kp.WoodBeam);
            }
            else
            {
                for (int i = 0; i < bays; i++)
                {
                    double u = -half + margin + bay * (i + 0.5);
                    if (door && i == bays / 2) Kp.Doorway(m, k, u, v0 + 0.35, 1.1, Math.Min(2.3, f1 - v0 - 0.9), Kp.Wood, false, lod);
                    else Kp.Lattice(m, k, u, v0 + 0.5 * (f1 + v0) + 0.1, Math.Min(0.8, 0.45 * bay), Math.Min(1.1, f1 - v0 - 1.4), lod, 2);
                }
            }
            for (int fl = 0; fl < nFloors; fl++)
            {
                double lv = floors[fl], next = fl + 1 < nFloors ? floors[fl + 1] : eave;
                Kp.Band(m, k, -half, half, lv - 0.1, lv + 0.12, 0.1, Kp.Wood);
                if (lod == 0) Kp.Band(m, k, -half, half, lv + 0.12, lv + 0.2, 0.05, Kp.WoodMid);
                double h = next - lv;
                if (fl == bandFloor)
                {
                    // A continuous carved lattice band between slim posts.
                    if (lod >= 2)
                    {
                        Kp.Panel(m, k, -half + 0.2, lv + 0.35, half - 0.2, next - 0.35, 0.03, Kp.Recess);
                        continue;
                    }
                    int n = Math.Max(3, (int)Math.Round(2 * half / 0.9));
                    for (int i = 0; i < n; i++)
                    {
                        double u = -half + 0.2 + (2 * half - 0.4) * (i + 0.5) / n;
                        Kp.Lattice(m, k, u, lv + 0.5 * h, (2 * half - 0.4) / n - 0.2, h - 0.7, lod, lod == 0 ? 2 : 0);
                    }
                    continue;
                }
                double wh = Math.Min(1.35, h - 1.0);
                if (wh < 0.5) continue;
                for (int i = 0; i < bays; i++)
                {
                    double u = -half + margin + bay * (i + 0.5);
                    if (fl == bayFloor && Math.Abs(u) < 0.6 * bay)
                    {
                        Kp.Bay(m, k, 0, lv + 0.35, Math.Min(3.0, 2.2 * bay), Math.Min(2.0, h - 0.5), false, lod);
                        continue;
                    }
                    if (fl == bayFloor && Math.Abs(u) < 1.6 * bay) continue;
                    Kp.Lattice(m, k, u, lv + 0.5 + 0.5 * wh, Math.Min(0.85, 0.5 * bay), wh, lod, 3);
                }
            }
            Kp.Cornice(m, k, -half, half, eave - 0.55, 0.42, Kp.Wood, lod);
        }

        /// <summary>
        /// A palace range: a grid block's walls from the square to just under its eave and its hipped tile roof (ridge
        /// along the longer side, <paramref name="over"/> metres of eave); returns the roof (for struts). The faces are
        /// dressed by the caller.
        /// </summary>
        public static RoofSpec Range(MeshData m, ref KpFrame f, double a0, double a1, double b0, double b1, double eave, double ridge, double over,
                                     in ShapeBrush wall, in ShapeBrush tile, bool fringe, int lod)
        {
            Walls(m, ref f, a0, a1, b0, b1, 0, eave + 0.2, wall);
            return Roof(m, ref f, a0, a1, b0, b1, eave, ridge, over, tile, fringe, lod);
        }

        /// <summary>A grid block's walls from v0 to v1 (no top: a roof covers it; the bottom sunk 0.2 m).</summary>
        public static void Walls(MeshData m, ref KpFrame f, double a0, double a1, double b0, double b1, double v0, double v1, in ShapeBrush b)
        {
            Kp.Box(m, f.Grid, a0, a1, v0 - 0.2, v1, b0, b1, b, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
        }

        /// <summary>The plan frame a grid block's roof is built in (its centre on the square's level): the frame for the
        /// struts and bells under that roof.</summary>
        public static Affine3 RoofFrame(ref KpFrame f, double a0, double a1, double b0, double b1)
        {
            return SacredDraw.At(f.Grid, 0.5 * (a0 + a1), 0, 0.5 * (b0 + b1));
        }

        /// <summary>A grid block's tile roof (hipped, ridge along its longer side).</summary>
        public static RoofSpec Roof(MeshData m, ref KpFrame f, double a0, double a1, double b0, double b1, double eave, double ridge, double over, in ShapeBrush tile,
                                    bool fringe, int lod)
        {
            Affine3 k = RoofFrame(ref f, a0, a1, b0, b1);
            return Kp.Ridge(m, k, 0.5 * (a1 - a0), 0.5 * (b1 - b0), eave, ridge, over, tile, fringe, lod);
        }

        /// <summary>A paved court floor (grey flags) over a grid rectangle, a hair above the level.</summary>
        public static void Court(MeshData m, ref KpFrame f, double a0, double a1, double b0, double b1, in ShapeBrush b)
        {
            Kp.Floor(m, f.Grid, a0, a1, b0, b1, 0.03, b);
        }
    }
}
