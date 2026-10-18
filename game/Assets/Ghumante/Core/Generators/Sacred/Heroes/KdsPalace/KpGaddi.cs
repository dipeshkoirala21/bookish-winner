using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// Gaddi Baithak (1908, Rana neoclassical; kds-palace.md §3.17, §3.18) on the palace grid, from its OSM parts: the main
    /// hall (41.4 × 16.3 m) whose south front on Basantapur carries paired engaged giant columns over two storeys of tall
    /// arched teal windows, projecting end bays, a balustraded parapet with a small central gable and the dark red-brown
    /// pitched roof behind; the west portico of six giant columns (pairs at the ends, two singles between) on tall
    /// pedestals over a 4 m balustraded podium, the iron candelabra on the podium corners, the entablature and
    /// the attic with its segmental crest; the white Hnulunche range north of it (pierced parapet, teal-shuttered windows,
    /// the dark carved oriel, the red-framed arched gateway) and the white ranges round Nucha Chowk. Lime white, teal
    /// glazing, black iron: no other colour.
    /// </summary>
    internal static class KpGaddi
    {
        /// <summary>The main hall (palace grid; OSM w430743883) and its cornice, parapet and ridge.</summary>
        public const double HA0 = -74.3, HA1 = -32.7, HB0 = -80.6, HB1 = -64.2, Cornice = 17.4, Parapet = 18.7, RidgeV = 21.0;

        /// <summary>The portico's axis (palace grid b), the podium's top and depth, the column line's distance out.</summary>
        public const double PortB = -71.4, PodiumTop = 4.0, PodiumDepth = 4.3, ColumnW = 1.6;

        /// <summary>The portico's columns (offsets along the front from its axis; pairs at the ends, two singles between).</summary>
        private static readonly double[] ColumnU = { -6.95, -5.95, -2.35, 1.85, 5.95, 6.95 };

        /// <summary>The Hnulunche west range (OSM w420797472).</summary>
        public const double NA0 = -77.5, NA1 = -70.2, NB0 = -62.5, NB1 = -44.0, NEave = 10.8;

        private const double PedTop = 6.2, ShaftTop = 14.6, CapTop = 15.1, EntTop = 16.5, AtticTop = 18.6;

        /// <summary>The dark grey of the balustrade's piercings.</summary>
        private static readonly ShapeBrush Pierce = new ShapeBrush(MeshColor.FromHex(0x4A4844), MaterialChannel.Plain, 1f);

        public static void Build(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            if (lod >= 3)
            {
                Kp.Box(m, g, HA0, HA1, 0, Parapet, HB0, HB1, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 0.5 * (HA0 + HA1), 0.5 * (HB0 + HB1), 20.8, 8.2, Parapet, RidgeV, Kp.GaddiRoof);
                Affine3 pk = f.GridFace(HA0, PortB, 0, 282);
                Kp.Box(m, pk, -8.8, 8.8, 0, PodiumTop, 0, PodiumDepth, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Bottom);
                Kp.Box(m, pk, -7.4, 7.4, PodiumTop, AtticTop, 0.6, 2.4, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Bottom);
                Kp.Box(m, g, NA0, NA1, 0, NEave + 1.0, NB0, NB1, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            Hall(ref f, m);
            Portico(ref f, m);
            Hnulunche(ref f, m);
            // The white ranges round Nucha Chowk behind.
            KpFacade.Range(m, ref f, NA1, -67.0, HB1, NB1, 10.0, 12.5, 0.9, Kp.GaddiWhite, Kp.GaddiRoof, false, lod);
            KpFacade.Range(m, ref f, -67.0, -55.7, -50.0, NB1, 10.0, 12.5, 0.9, Kp.GaddiWhite, Kp.GaddiRoof, false, lod);
            KpFacade.Range(m, ref f, -58.5, -55.7, HB1, -50.0, 10.0, 12.5, 0.9, Kp.GaddiWhite, Kp.GaddiRoof, false, lod);
            KpFacade.Court(m, ref f, -67.0, -58.5, HB1, -50.0, Kp.Flags);
            if (f.C != null)
            {
                // The hall behind its loggia, the two end bays and the giant columns.
                f.GridBox(HA0, HA1, HB0 + LoggiaBack, HB1, 0, RidgeV, GenColliderFlags.NoClimb, GenColliders.Stone);
                f.GridBox(HA0, HA0 + 5.5, HB0 - 0.6, HB0 + LoggiaBack, 0, RidgeV, GenColliderFlags.NoClimb, GenColliders.Stone);
                f.GridBox(HA1 - 5.5, HA1, HB0 - 0.6, HB0 + LoggiaBack, 0, RidgeV, GenColliderFlags.NoClimb, GenColliders.Stone);
                double bay = (HA1 - HA0 - 11.0) / 7;
                for (int i = 0; i <= 7; i++)
                {
                    double cx, cy;
                    KpFrame.FromGrid(HA0 + 5.5 + bay * i, HB0 - ColumnLine, out cx, out cy);
                    f.Cylinder(cx, cy, SouthPodium, CapTop, 0.65);
                }
                f.GridBox(NA0, NA1, NB0, NB1, 0, 13.0, GenColliderFlags.NoClimb, GenColliders.Stone);
                f.GridBox(NA1, -55.7, HB1, NB1, 0, 12.5, GenColliderFlags.NoClimb, GenColliders.Stone);
            }
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The hall and its south front
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>The south front's loggia: how far the hall's south wall stands behind the column line's face (the OSM
        /// edge), the podium's front edge out from it, the podium's top, and the column line.</summary>
        /// <summary>The south loggia: how far the wall stands back behind the podium edge (the column line).</summary>
        public const double LoggiaBack = 2.5;

        private const double PodiumOut = 0.3, SouthPodium = 2.0, ColumnLine = -0.45;

        private static void Hall(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            // The hall's walls: the south wall set back behind the giant colonnade (gaddi ref_00, ref_03, ref_05).
            Kp.Box(m, g, HA0, HA1, -0.2, Cornice, HB0 + LoggiaBack, HB1, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            // The red-brown pitched roof behind the parapet.
            Affine3 rk = KpFacade.RoofFrame(ref f, HA0, HA1, HB0, HB1);
            Kp.Ridge(m, rk, 0.5 * (HA1 - HA0) - 0.4, 0.5 * (HB1 - HB0) - 0.4, Cornice + 0.2, RidgeV, 0.0, Kp.GaddiRoof, false, Math.Max(1, lod));
            double half;
            Affine3 s = f.GridSide(HA0, HA1, HB0, HB1, 0, 2, out half);
            // The podium along the front (2 m, a moulded base and cornice), the loggia floor on it.
            Kp.Box(m, s, -half - 0.6, half + 0.6, -0.2, SouthPodium, -LoggiaBack, PodiumOut, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Kp.Band(m, SacredDraw.At(s, 0, 0, PodiumOut), -half - 0.6, half + 0.6, 0, 0.4, 0.08, Kp.GaddiShade);
            Kp.Band(m, SacredDraw.At(s, 0, 0, PodiumOut), -half - 0.7, half + 0.7, SouthPodium - 0.25, SouthPodium, 0.12, Kp.GaddiShade);
            // The projecting end bays, closed from the hall wall out past the column line.
            const double endW = 5.5, proj = 0.6;
            for (int e = -1; e <= 1; e += 2)
            {
                double u0 = e < 0 ? -half : half - endW, u1 = e < 0 ? -half + endW : half;
                Kp.Box(m, s, u0, u1, SouthPodium - 0.2, Cornice - 0.4, -LoggiaBack, proj, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                Affine3 eb = SacredDraw.At(s, 0, 0, proj);
                double uc = 0.5 * (u0 + u1);
                TealArch(m, eb, uc, SouthPodium + 0.8, 1.7, 4.0, lod);
                TealArch(m, eb, uc, 8.8, 1.7, 4.4, lod);
                if (lod <= 1)
                    for (int q = -1; q <= 1; q += 2)
                        EngagedColumn(m, eb, uc + q * 2.15, SouthPodium, 15.3, 0.36, lod);
            }
            // The centre: seven bays; on the recessed wall shaded round-headed openings with white doors on both storeys,
            // the giant columns standing proud at the podium's edge, pairs at the ends (the west portico's rhythm).
            int bays = 7;
            double c0 = -half + endW, c1 = half - endW, bay = (c1 - c0) / bays;
            Affine3 back = SacredDraw.At(s, 0, 0, -LoggiaBack);
            // The loggia's back wall lies in the colonnade's shade (gaddi ref_00, ref_05: grey behind the white columns).
            Kp.Panel(m, back, c0, SouthPodium, c1, CapTop, 0.004, Kp.Ao(Kp.GaddiShade, lod >= 2 ? 0.8f : 0.7f));
            for (int i = 0; i < bays; i++)
            {
                double uc = c0 + bay * (i + 0.5);
                LoggiaOpening(m, back, uc, SouthPodium, 2.1, 4.7, lod);
                LoggiaOpening(m, back, uc, 8.6, 1.9, 4.3, lod);
                if (lod <= 1) Balustrade(m, back, uc - 1.3, uc + 1.3, 0.45, 7.7, 0.8, lod);
            }
            // The first-floor band across the back wall.
            Kp.Box(m, back, c0, c1, 7.35, 7.7, 0, 0.55, Kp.GaddiShade, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            for (int i = 0; i <= bays; i++)
            {
                double u = c0 + bay * i;
                StandingColumn(m, s, u, lod);
                if (i == 0) StandingColumn(m, s, u + 1.05, lod);
                if (i == bays) StandingColumn(m, s, u - 1.05, lod);
            }
            // Entablature over the colonnade (its underside the loggia's ceiling), cornice, the balustraded parapet and
            // the central gable.
            Kp.Box(m, s, -half - 0.05, half + 0.05, CapTop, 16.9, -LoggiaBack, proj + 0.12, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, s, -half - 0.05, half + 0.05, CapTop - 0.02, CapTop + 0.3, ColumnLine - 0.75, proj + 0.14, Kp.GaddiShade, BoxFaces.Front | BoxFaces.Bottom);
            Kp.Box(m, s, -half - 0.25, half + 0.25, 16.9, Cornice, -LoggiaBack, proj + 0.45, Kp.GaddiShade, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Balustrade(m, s, -half, half, proj + 0.2, Cornice, Parapet - Cornice, lod);
            Kp.Box(m, s, -3.4, 3.4, Cornice, Parapet + 0.05, proj, proj + 0.3, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Tri(m, s, -3.6, Parapet + 0.05, proj + 0.3, 3.6, Parapet + 0.05, proj + 0.3, 0, Parapet + 1.5, proj + 0.3, 0, 0, 1, Kp.GaddiWhite);
            if (lod <= 1)
            {
                SacredDraw.Quad(m, s, -3.6, Parapet + 0.05, proj + 0.3, 0, Parapet + 1.5, proj + 0.3, 0, Parapet + 1.5, -1.0, -3.6, Parapet + 0.05, -1.0, -0.4, 1, 0,
                                Kp.GaddiShade);
                SacredDraw.Quad(m, s, 3.6, Parapet + 0.05, proj + 0.3, 0, Parapet + 1.5, proj + 0.3, 0, Parapet + 1.5, -1.0, 3.6, Parapet + 0.05, -1.0, 0.4, 1, 0,
                                Kp.GaddiShade);
            }
            // The east and north faces: plain arched windows in two storeys.
            if (lod <= 1)
            {
                Affine3 n = f.GridSide(HA0, HA1, HB0, HB1, 0, 0, out half);
                for (int i = 0; i < 8; i++)
                {
                    double u = -half + 2 * half * (i + 0.5) / 8;
                    TealArch(m, n, u, 9.0, 1.6, 3.8, Math.Max(1, lod));
                }
            }
            if (f.C != null)
            {
                // The podium is walkable up to the loggia; the columns are solid.
                double x, y;
                KpFrame.FromGrid(0.5 * (HA0 + HA1), HB0 - 0.5 * (PodiumOut - LoggiaBack), out x, out y);
                f.Box(x, y, 0.5 * (HA1 - HA0) + 0.6, 0.5 * (PodiumOut + LoggiaBack), 102, 0, SouthPodium, GenColliderFlags.Walkable, GenColliders.Stone);
            }
        }

        /// <summary>A free-standing giant column of the south front at face u: a tall moulded pedestal from the podium to
        /// 6.2 m, the shaft (0.9 m across) to 14.6 m, an Ionic capital (scrolls and echinus at LOD0).</summary>
        private static void StandingColumn(MeshData m, in Affine3 s, double u, int lod)
        {
            double w = ColumnLine;
            Kp.Box(m, s, u - 0.62, u + 0.62, SouthPodium, PedTop, w - 0.62, w + 0.62, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                Kp.Box(m, s, u - 0.72, u + 0.72, PedTop - 0.25, PedTop, w - 0.72, w + 0.72, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Bottom);
                Kp.Box(m, s, u - 0.7, u + 0.7, SouthPodium, SouthPodium + 0.3, w - 0.7, w + 0.7, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Bottom);
            }
            Column(m, SacredDraw.At(s, u, 0, w), PedTop, ShaftTop, 0.45, lod);
            Kp.Box(m, s, u - 0.7, u + 0.7, ShaftTop, CapTop, w - 0.7, w + 0.7, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Top);
            if (lod > 0) return;
            for (int e = -1; e <= 1; e += 2)
                Shapes.Cylinder(m, SacredDraw.Basis(s, u + e * 0.5, ShaftTop - 0.12, w - 0.62, 1, 0, 0, 0, 0, 1), Kp.GaddiWhite, 0.2, 1.24, 8, 0.04, 1, true, true);
            Shapes.Torus(m, SacredDraw.At(s, u, ShaftTop - 0.2, w), Kp.GaddiShade, 0.46, 0.08, 12, 4);
        }

        /// <summary>A round-headed opening in the loggia's back wall (the wall in shade): the shaded reveal, a pair of white
        /// panelled doors (glazed only in the fan light) and the moulded archivolt. Sill at v0, width ww, crown at v0 + h.</summary>
        private static void LoggiaOpening(MeshData m, in Affine3 k, double u, double v0, double ww, double h, int lod)
        {
            double r = 0.5 * ww, rect = h - r;
            ShapeBrush shade = Kp.Ao(Kp.GaddiShade, 0.62f);
            if (lod >= 2)
            {
                Kp.Panel(m, k, u - r, v0, u + r, v0 + rect, 0.01, Kp.Ao(Kp.WhiteTrim, 0.7f));
                return;
            }
            // The white doors (two leaves with panels) and the shaded fan light over them.
            Kp.Panel(m, k, u - r, v0, u + r, v0 + rect, 0.012, Kp.Ao(Kp.WhiteTrim, 0.78f));
            Kp.HalfDisc(m, k, u, v0 + rect, r, r, 0.012, shade, lod == 0 ? 10 : 6);
            Kp.Box(m, k, u - r - 0.16, u - r, v0, v0 + rect, 0, 0.12, Kp.WhiteTrim, BoxFaces.Wall);
            Kp.Box(m, k, u + r, u + r + 0.16, v0, v0 + rect, 0, 0.12, Kp.WhiteTrim, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, k, u, v0 + rect, 0.012, ww, r, 0.16, 0.12, false, Kp.WhiteTrim, lod + 1);
            if (lod > 0) return;
            Kp.Box(m, k, u - 0.025, u + 0.025, v0, v0 + rect, 0.012, 0.05, Kp.Ao(Kp.GaddiShade, 0.8f), BoxFaces.Front);
            for (int sd = -1; sd <= 1; sd += 2)
                for (int q = 0; q < 2; q++)
                {
                    double pu = u + sd * 0.5 * r, pv = v0 + 0.35 + q * 0.5 * rect;
                    Kp.Box(m, k, pu - 0.3 * r, pu + 0.3 * r, pv, pv + 0.4 * rect - 0.2, 0.012, 0.035, Kp.Ao(Kp.WhiteTrim, 0.7f), BoxFaces.Front);
                }
            Kp.Box(m, k, u - r, u + r, v0 + rect - 0.08, v0 + rect, 0.012, 0.05, Kp.Ao(Kp.WhiteTrim, 0.85f), BoxFaces.Front | BoxFaces.Top);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The west portico
        // ---------------------------------------------------------------------------------------------------------------

        private static void Portico(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 k = f.GridFace(HA0, PortB, 0, 282);
            const double ph = 8.8;
            // The podium (OSM w420797500: 4 m) with its plain white face, a moulded base and cornice, and the pierced
            // balustrade along its top (photos gaddi c_00, c_08, c_09, c_16: no stair on the west front).
            Kp.Box(m, k, -ph, ph, -0.2, PodiumTop, 0, PodiumDepth, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Kp.Band(m, k, -ph, ph, 0, 0.45, 0.08, Kp.GaddiShade);
            Kp.Box(m, SacredDraw.At(k, 0, 0, PodiumDepth), -ph - 0.1, ph + 0.1, PodiumTop - 0.3, PodiumTop, -0.05, 0.12, Kp.GaddiShade, BoxFaces.Wall | BoxFaces.Bottom | BoxFaces.Top);
            if (lod <= 1)
                for (int e = -1; e <= 1; e += 2)
                    Kp.Box(m, SacredDraw.At(k, 0, 0, PodiumDepth), e * 5.2 - 1.9, e * 5.2 + 1.9, 0.9, PodiumTop - 0.7, 0, 0.05, Kp.GaddiShade, BoxFaces.Front);
            Balustrade(m, SacredDraw.At(k, 0, 0, 0), -ph, ph, PodiumDepth - 0.08, PodiumTop, 0.95, lod);
            for (int e = -1; e <= 1; e += 2)
            {
                Affine3 side = SacredDraw.Basis(k, e * (ph - 0.08), 0, 0, 0, 0, e, 0, 1, 0);
                Balustrade(m, side, -PodiumDepth, 0, 0, PodiumTop, 0.95, lod);
            }
            // The wall behind: three tall arched recesses between the columns (photos gaddi c_08, c_09, c_16), each with a
            // pedimented teal door below and an arched teal window above.
            double[] bays = { -4.15, 0.0, 4.15 };
            for (int i = 0; i < 3; i++) ArchBay(m, k, bays[i], PodiumTop, 13.2, 3.3, lod);
            ArchFront(m, k, bays, PodiumTop, 13.2, 3.3, lod);
            // Pedestals (OSM: to 6.2 m), shafts with bases and capitals.
            for (int i = 0; i < ColumnU.Length; i++)
            {
                double u = ColumnU[i];
                Kp.Box(m, k, u - 0.66, u + 0.66, PodiumTop, PedTop, ColumnW - 0.66, ColumnW + 0.66, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Bottom);
                if (lod <= 1) Kp.Box(m, k, u - 0.76, u + 0.76, PedTop - 0.25, PedTop, ColumnW - 0.76, ColumnW + 0.76, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Bottom);
                Column(m, SacredDraw.At(k, u, 0, ColumnW), PedTop, ShaftTop, 0.45, lod);
                Kp.Box(m, k, u - 0.7, u + 0.7, ShaftTop, CapTop, ColumnW - 0.7, ColumnW + 0.7, Kp.GaddiWhite, BoxFaces.All);
                if (lod == 0)
                {
                    // Ionic capital: the two scrolls (volutes) either side under the abacus, and the echinus ring.
                    for (int e = -1; e <= 1; e += 2)
                        Shapes.Cylinder(m, SacredDraw.Basis(k, u + e * 0.5, ShaftTop - 0.12, ColumnW - 0.62, 1, 0, 0, 0, 0, 1), Kp.GaddiWhite, 0.2, 1.24, 8, 0.04, 1, true, true);
                    Shapes.Torus(m, SacredDraw.At(k, u, ShaftTop - 0.2, ColumnW), Kp.GaddiShade, 0.46, 0.08, 12, 4);
                }
            }
            // The entablature over the columns, the attic and its segmental crest with urns.
            Kp.Box(m, k, -7.7, 7.7, CapTop, EntTop, -0.2, ColumnW + 0.85, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Back);
            Kp.Box(m, k, -7.95, 7.95, EntTop - 0.3, EntTop, -0.2, ColumnW + 1.1, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Back);
            Kp.Box(m, k, -8.2, 8.2, EntTop, AtticTop, -0.2, ColumnW + 0.6, Kp.GaddiWhite, BoxFaces.All & ~BoxFaces.Back);
            Kp.Box(m, k, -8.4, 8.4, AtticTop - 0.25, AtticTop, -0.2, ColumnW + 0.8, Kp.GaddiShade, BoxFaces.All & ~BoxFaces.Back);
            Crest(m, SacredDraw.At(k, 0, AtticTop, ColumnW + 0.6), 2.5, 1.0, lod);
            if (lod <= 1)
            {
                // The row of urns along the attic over the columns (gaddi c_09), the corner ones larger.
                if (lod == 0)
                    for (int i = 0; i < ColumnU.Length; i++)
                        if (i != 0 && i != ColumnU.Length - 1) Urn(m, SacredDraw.At(k, ColumnU[i], AtticTop, ColumnW + 0.3) * Affine3.Scaling(0.75), lod);
                for (int e = -1; e <= 1; e += 2)
                {
                    Urn(m, SacredDraw.At(k, e * 7.6, AtticTop, ColumnW), lod);
                    // The scroll volutes either side of the crest.
                    Shapes.Torus(m, SacredDraw.Basis(k, e * 3.0, AtticTop + 0.45, ColumnW + 0.3, 0, 0, 1, 0, 1, 0), Kp.GaddiWhite, 0.38, 0.12, lod == 0 ? 14 : 8, 6,
                                 new ShapeLod(lod), e > 0 ? 180 : -90, 270, true);
                }
                // Panels on the attic face.
                for (int e = -1; e <= 1; e += 2)
                    Kp.Box(m, k, e * 5.4 - 1.4, e * 5.4 + 1.4, EntTop + 0.5, AtticTop - 0.6, ColumnW + 0.6, ColumnW + 0.66, Kp.GaddiShade, BoxFaces.Front);
                // The cast-iron candelabra on the podium corners.
                for (int e = -1; e <= 1; e += 2) Lamp(m, SacredDraw.At(k, e * (ph - 0.6), PodiumTop, PodiumDepth - 0.6), lod);
            }
            if (f.C == null) return;
            double x, y;
            KpFrame.FromGrid(HA0 - 0.5 * PodiumDepth, PortB, out x, out y);
            f.Box(x, y, 0.5 * PodiumDepth, ph, 102, 0, PodiumTop, GenColliderFlags.Walkable, GenColliders.Stone);
            double x0, y0, x1, y1;
            for (int i = 0; i < ColumnU.Length; i++)
            {
                double cx, cy;
                KpFrame.FromGrid(HA0 - ColumnW, PortB - ColumnU[i], out cx, out cy);
                f.Cylinder(cx, cy, PodiumTop, EntTop, 0.66);
            }
            // The balustrade along the podium's edge (the camera passes over it).
            KpFrame.FromGrid(HA0 - PodiumDepth + 0.08, PortB - ph, out x0, out y0);
            KpFrame.FromGrid(HA0 - PodiumDepth + 0.08, PortB + ph, out x1, out y1);
            f.Wall(x0, y0, x1, y1, PodiumTop, PodiumTop + 0.95, 0.08);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Hnulunche west range
        // ---------------------------------------------------------------------------------------------------------------

        private static void Hnulunche(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            Kp.Box(m, g, NA0, NA1, -0.2, NEave + 0.2, NB0, NB1, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Ridge(m, KpFacade.RoofFrame(ref f, NA0, NA1, NB0, NB1), 0.5 * (NA1 - NA0) - 0.3, 0.5 * (NB1 - NB0) - 0.3, NEave + 0.1, NEave + 2.4, 0.0, Kp.GaddiRoof, false,
                     Math.Max(1, lod));
            double half;
            Affine3 w = f.GridSide(NA0, NA1, NB0, NB1, 0, 3, out half);
            Kp.Band(m, w, -half, half, 0, 0.45, 0.05, Kp.GaddiShade);
            Kp.Band(m, w, -half, half, 5.0, 5.2, 0.12, Kp.WhiteTrim);
            Kp.Box(m, w, -half - 0.1, half + 0.1, NEave - 0.5, NEave, 0, 0.3, Kp.GaddiShade, BoxFaces.Wall | BoxFaces.Bottom);
            Balustrade(m, w, -half, half, 0.15, NEave, 1.0, lod);
            const double oriel = -1.0, gate = 2.2;
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                double u = -half + 2 * half * (i + 0.5) / n;
                if (Math.Abs(u - oriel) > 1.6) Kp.RanaWindow(m, w, u, 6.2, 0.95, 1.7, Kp.Teal, false, lod);
                if (Math.Abs(u - gate) > 1.9) Kp.RanaWindow(m, w, u, 1.4, 0.9, 1.6, Kp.Teal, true, lod);
            }
            // The dark carved timber oriel at first-floor centre and the red-framed round-arched gateway.
            Kp.Bay(m, w, oriel, 5.9, 2.6, 2.1, false, lod);
            Kp.Panel(m, w, gate - 1.1, 0.0, gate + 1.1, 2.9, 0.01, Kp.Recess);
            Kp.Box(m, w, gate - 1.35, gate - 1.1, 0, 2.9, 0, 0.18, Kp.Dado, BoxFaces.Wall);
            Kp.Box(m, w, gate + 1.1, gate + 1.35, 0, 2.9, 0, 0.18, Kp.Dado, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, w, gate, 2.9, 0.012, 2.2, 1.1, 0.25, 0.18, false, Kp.Dado, lod);
            Kp.Panel(m, w, gate - 1.1, 2.9, gate + 1.1, 3.95, 0.006, Kp.Recess);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Parts
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>The depth of the portico's arched recesses (the arcade face stands this far out from the hall wall).</summary>
        private const double ArchDepth = 0.9;

        /// <summary>
        /// A tall arched recess of the portico (photos gaddi c_08, c_09): the round-headed opening of width
        /// <paramref name="width"/> from v0 to <paramref name="top"/> (crown) in the arcade face that stands
        /// <see cref="ArchDepth"/> out from the hall wall, with its moulded archivolt and soffit; inside, on the hall wall, a
        /// teal door under a small triangular pediment and an arched teal window above.
        /// </summary>
        private static void ArchBay(MeshData m, in Affine3 k, double u, double v0, double top, double width, int lod)
        {
            double r = 0.5 * width, spring = top - r;
            Affine3 back = SacredDraw.At(k, 0, 0, 0.01);
            if (lod >= 2)
            {
                Kp.Panel(m, back, u - r, v0, u + r, spring, 0.0, Kp.Ao(Kp.GaddiShade, 0.7f));
                TealArch(m, back, u, v0 + 0.3, 1.5, 3.4, lod);
                return;
            }
            // The recess lies in deep shade behind the columns (gaddi c_08, p6: grey against the sunlit white).
            Kp.Panel(m, back, u - r, v0, u + r, spring, 0.0, Kp.Ao(Kp.GaddiShade, 0.62f));
            Kp.HalfDisc(m, back, u, spring, r, r, 0.0, Kp.Ao(Kp.GaddiShade, 0.56f), lod == 0 ? 10 : 6);
            ShikharaGenerator.Arch(m, k, u, spring, ArchDepth, width, r, 0.32, ArchDepth, false, Kp.GaddiWhite, lod);
            // Inside: the door with its pediment, the arched window above.
            Kp.Panel(m, back, u - 0.75, v0, u + 0.75, v0 + 3.3, 0.02, Kp.Tint(Kp.Teal, 0.85f));
            Kp.Box(m, back, u - 0.95, u - 0.75, v0, v0 + 3.3, 0, 0.12, Kp.WhiteTrim, BoxFaces.Wall);
            Kp.Box(m, back, u + 0.75, u + 0.95, v0, v0 + 3.3, 0, 0.12, Kp.WhiteTrim, BoxFaces.Wall);
            Kp.Box(m, back, u - 1.1, u + 1.1, v0 + 3.3, v0 + 3.5, 0, 0.2, Kp.WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Tri(m, back, u - 1.1, v0 + 3.5, 0.2, u + 1.1, v0 + 3.5, 0.2, u, v0 + 4.15, 0.2, 0, 0, 1, Kp.WhiteTrim);
            if (lod == 0)
            {
                SacredDraw.Quad(m, back, u - 1.1, v0 + 3.5, 0.2, u, v0 + 4.15, 0.2, u, v0 + 4.15, 0.0, u - 1.1, v0 + 3.5, 0.0, -0.5, 0.85, 0, Kp.GaddiShade);
                SacredDraw.Quad(m, back, u + 1.1, v0 + 3.5, 0.2, u, v0 + 4.15, 0.2, u, v0 + 4.15, 0.0, u + 1.1, v0 + 3.5, 0.0, 0.5, 0.85, 0, Kp.GaddiShade);
                for (int i = 1; i < 4; i++) Kp.Box(m, back, u - 0.75, u + 0.75, v0 + 0.8 * i, v0 + 0.8 * i + 0.06, 0.02, 0.06, Kp.Teal, BoxFaces.Front);
            }
            TealArch(m, SacredDraw.At(back, 0, 0, 0.012), u, v0 + 5.2, 1.7, Math.Min(3.9, spring - v0 - 5.4 + r), lod);
        }

        /// <summary>The arcade face round the arched recesses: piers between and beside the openings (their sides are the
        /// reveals) and the band over the arches up to the capitals.</summary>
        private static void ArchFront(MeshData m, in Affine3 k, double[] bays, double v0, double top, double width, int lod)
        {
            double r = 0.5 * width, crown = top + 0.32, d = lod >= 2 ? 0.05 : ArchDepth - 0.01;
            double prev = -8.2;
            for (int i = 0; i <= bays.Length; i++)
            {
                double next = i < bays.Length ? bays[i] - r : 8.2;
                if (next - prev > 0.02)
                    Kp.Box(m, k, prev, next, v0, crown, 0, d, Kp.GaddiWhite, lod >= 2 ? BoxFaces.Front : BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                if (i < bays.Length) prev = bays[i] + r;
            }
            Kp.Box(m, k, -8.2, 8.2, crown, CapTop, 0, d, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
        }

        /// <summary>A tall round-arched opening (sill at v0, total height h with the arch): white moulded surround and arch,
        /// the opening a deep shaded recess behind slim dark-green painted frame bars (gaddi ref_00, ref_03, ref_05: the
        /// openings read dark from the square, no glazing shows).</summary>
        /// <summary>The shaded depth of the tall arched openings.</summary>
        private static readonly ShapeBrush ArchShade = new ShapeBrush(MeshColor.FromHex(0x2A302E), MaterialChannel.Plain, 0.85f);

        /// <summary>The dark-green painted frame bars in them.</summary>
        private static readonly ShapeBrush ArchBar = new ShapeBrush(MeshColor.FromHex(0x24443C), MaterialChannel.Paint, 1f);

        private static void TealArch(MeshData m, in Affine3 k, double u, double v0, double ww, double h, int lod)
        {
            double r = 0.5 * ww, rect = h - r;
            if (lod >= 2)
            {
                Kp.Panel(m, k, u - r, v0, u + r, v0 + rect, 0.015, ArchShade);
                return;
            }
            Kp.Panel(m, k, u - r, v0, u + r, v0 + rect, 0.012, ArchShade);
            // The moulded arch at LOD0 (LOD1 keeps the glazed fan and the frame).
            if (lod == 0) ShikharaGenerator.Arch(m, k, u, v0 + rect, 0.008, ww, r, 0.14, 0.1, false, Kp.WhiteTrim, lod);
            // The arch's shaded head (a fan of triangles) and the frame.
            int seg = lod == 0 ? 8 : 4;
            for (int i = 0; i < seg; i++)
            {
                double a0 = Math.PI * i / seg, a1 = Math.PI * (i + 1) / seg;
                SacredDraw.Tri(m, k, u, v0 + rect, 0.012, u + r * Math.Cos(a0), v0 + rect + r * Math.Sin(a0), 0.012, u + r * Math.Cos(a1), v0 + rect + r * Math.Sin(a1), 0.012,
                               0, 0, 1, Kp.Tint(ArchShade, 1.12f));
            }
            Kp.Box(m, k, u - r - 0.14, u - r, v0, v0 + rect, 0, 0.1, Kp.WhiteTrim, BoxFaces.Wall);
            Kp.Box(m, k, u + r, u + r + 0.14, v0, v0 + rect, 0, 0.1, Kp.WhiteTrim, BoxFaces.Wall);
            Kp.Box(m, k, u - r - 0.25, u + r + 0.25, v0 - 0.15, v0, 0, 0.16, Kp.WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod > 0) return;
            // The dark-green painted frame bars.
            Kp.Box(m, k, u - 0.04, u + 0.04, v0, v0 + rect + r, 0.012, 0.05, ArchBar, BoxFaces.Front);
            for (int i = 1; i < 3; i++) Kp.Box(m, k, u - r, u + r, v0 + rect * i / 3 - 0.04, v0 + rect * i / 3 + 0.04, 0.012, 0.05, ArchBar, BoxFaces.Front);
        }

        /// <summary>A giant column shaft with a moulded base (frame at its axis on the square's level).</summary>
        private static void Column(MeshData m, in Affine3 k, double v0, double v1, double r, int lod)
        {
            int seg = lod == 0 ? 14 : lod == 1 ? 7 : 5;
            var p = new Profile2(10);
            p.Add(0, 0, true).Add(r + 0.14, 0, true).Add(r + 0.14, 0.14, true).Add(r + 0.06, 0.26).Add(r, 0.4, true).Add(0.94 * r, v1 - v0 - 0.3).Add(r + 0.05, v1 - v0 - 0.15, true)
             .Add(r + 0.05, v1 - v0, true).Add(0, v1 - v0, true);
            Shapes.Lathe(m, SacredDraw.At(k, 0, v0, 0), Kp.GaddiWhite, lod >= 1 ? p : p.Smooth(1), seg, new ShapeLod(Math.Min(2, lod)));
        }

        /// <summary>An engaged half column on a wall face (u, from v0 to v1, radius r) with a base and capital block.</summary>
        private static void EngagedColumn(MeshData m, in Affine3 k, double u, double v0, double v1, double r, int lod)
        {
            Affine3 at = SacredDraw.Basis(k, u, v0, 0, 1, 0, 0, 0, 1, 0);
            if (lod == 0) Shapes.Lathe(m, at, Kp.GaddiWhite, HalfShaft(r, v1 - v0), 8, new ShapeLod(0), 270, 180, false);
            else Kp.Box(m, k, u - r, u + r, v0, v1, 0, 0.6 * r, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, u - r - 0.12, u + r + 0.12, v0, v0 + 0.45, 0, r + 0.12, Kp.GaddiShade, BoxFaces.Wall);
            Kp.Box(m, k, u - r - 0.15, u + r + 0.15, v1 - 0.4, v1, 0, r + 0.15, Kp.GaddiWhite, BoxFaces.Wall | BoxFaces.Bottom);
        }

        private static Profile2 HalfShaft(double r, double h)
        {
            var p = new Profile2(4);
            p.Add(r, 0, true).Add(0.95 * r, h, true);
            return p;
        }

        /// <summary>
        /// A pierced (quatrefoil) balustrade from u0 to u1 standing on v at depth w: a base, a top rail and a band of dark
        /// pierced openings between short white posts (LOD1 on: a shaded band).
        /// </summary>
        private static void Balustrade(MeshData m, in Affine3 k, double u0, double u1, double w, double v, double h, int lod)
        {
            Kp.Box(m, k, u0, u1, v, v + 0.12, w - 0.14, w + 0.08, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            Kp.Box(m, k, u0, u1, v + h - 0.12, v + h, w - 0.14, w + 0.08, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top | BoxFaces.Bottom);
            if (lod >= 1)
            {
                // LOD1 on: the pierced band reads as a shaded strip.
                Kp.Box(m, k, u0, u1, v + 0.12, v + h - 0.12, w - 0.08, w + 0.02, Kp.GaddiShade, BoxFaces.Front | BoxFaces.Back);
                return;
            }
            Kp.Box(m, k, u0, u1, v + 0.12, v + h - 0.12, w - 0.07, w + 0.0, Kp.GaddiWhite, BoxFaces.Front | BoxFaces.Back);
            const double pitch = 0.6;
            int n = Math.Max(1, (int)Math.Round((u1 - u0) / pitch));
            double cell = (u1 - u0) / n, hv = 0.5 * (h - 0.24), vc = v + 0.5 * h;
            ShapeBrush hole = Kp.Ao(Pierce, 0.85f);
            for (int i = 0; i < n; i++)
            {
                double uc = u0 + cell * (i + 0.5), d = Math.Min(0.3 * cell, 0.62 * hv), t = 0.32 * d;
                // The pierced cross-and-star of each panel (gaddi c_09, c_16): two slim crossed lozenges, dark grey through
                // the white stone, on both faces.
                for (int side = 0; side < (lod == 0 ? 2 : 1); side++)
                {
                    double ww = side == 0 ? w + 0.006 : w - 0.076, nz = side == 0 ? 1 : -1;
                    SacredDraw.Quad(m, k, uc - d, vc, ww, uc, vc - t, ww, uc + d, vc, ww, uc, vc + t, ww, 0, 0, nz, hole);
                    SacredDraw.Quad(m, k, uc - t, vc, ww, uc, vc - d, ww, uc + t, vc, ww, uc, vc + d, ww, 0, 0, nz, hole);
                }
            }
        }

        /// <summary>The attic's segmental crest: a low curved pediment (half width hw, rise) on the attic top, facing +w.</summary>
        private static void Crest(MeshData m, in Affine3 k, double hw, double rise, int lod)
        {
            int seg = lod == 0 ? 12 : 6;
            double R = (hw * hw + rise * rise) / (2 * rise), c = rise - R;
            for (int i = 0; i < seg; i++)
            {
                double u0 = -hw + 2 * hw * i / seg, u1 = -hw + 2 * hw * (i + 1) / seg;
                double v0 = c + Math.Sqrt(Math.Max(0, R * R - u0 * u0)), v1 = c + Math.Sqrt(Math.Max(0, R * R - u1 * u1));
                SacredDraw.Quad(m, k, u0, 0, 0, u1, 0, 0, u1, v1, 0, u0, v0, 0, 0, 0, 1, Kp.GaddiWhite);
                SacredDraw.Quad(m, k, u0, v0, 0, u1, v1, 0, u1, v1, -0.8, u0, v0, -0.8, -u0 / R, 1, 0, Kp.GaddiShade);
            }
            if (lod <= 1) Kp.Box(m, k, -0.45, 0.45, 0.2, 1.1, 0, 0.08, Kp.GaddiShade, BoxFaces.Front);
        }

        /// <summary>A white urn finial on the attic corner.</summary>
        private static void Urn(MeshData m, in Affine3 k, int lod)
        {
            var p = new Profile2(10);
            p.Add(0, 0, true).Add(0.32, 0, true).Add(0.32, 0.18, true).Add(0.18, 0.3).Add(0.36, 0.6).Add(0.3, 0.95).Add(0.14, 1.1).Add(0.2, 1.2).Add(0, 1.45, true);
            Shapes.Lathe(m, k, Kp.GaddiWhite, p.Smooth(lod == 0 ? 1 : 0), lod == 0 ? 10 : 6);
        }

        /// <summary>A black cast-iron candelabrum: a fluted post with four arms and globes, about 3.4 m.</summary>
        private static void Lamp(MeshData m, in Affine3 k, int lod)
        {
            Shapes.Cylinder(m, k, Kp.Iron, 0.22, 0.4, 8, 0, 0, false, true);
            Shapes.Frustum(m, SacredDraw.At(k, 0, 0.4, 0), Kp.Iron, 0.1, 0.06, 2.4, 6, 0, 0, false, false);
            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI * 0.5 * i + Math.PI * 0.25, c = Math.Cos(a), s = Math.Sin(a);
                Shapes.Bar(m, k, Kp.Iron, 0, 2.5, 0, 0.55 * c, 2.75, 0.55 * s, 0.03, 4);
                Shapes.Sphere(m, SacredDraw.At(k, 0.55 * c, 2.95, 0.55 * s), Kp.WhiteTrim, 0.16, lod == 0 ? 8 : 5);
            }
            Shapes.Sphere(m, SacredDraw.At(k, 0, 3.15, 0), Kp.WhiteTrim, 0.2, lod == 0 ? 8 : 5);
        }
    }
}
