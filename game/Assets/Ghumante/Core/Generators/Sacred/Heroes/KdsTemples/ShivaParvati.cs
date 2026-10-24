using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The Shiva-Parvati temple (Nava Yogini, late 18th c.; kds-temples.md T7; OSM w185882281 and its parts: plinth 14.7 ×
    /// 9.8 to 1.2 m and 11.8 × 7.0 to 1.4 m, ground storey 11.5 × 6.6 to 5.5 m, cornice band 12.1 × 7.2, upper storey
    /// 10.8 × 5.9 to 8 m, hipped roof 15.7 × 10.8 from 7 to 10 m, finials to 11.3 m): a long brick house-temple facing
    /// south-south-west toward Maju Dega. A stepped plinth with a broad central stair and two big stone lions on the
    /// lower stage; a ground storey of five carved bays on the front (the centre one the door, the others lattice windows,
    /// each a tall carved frame with a round-headed opening under a great semicircular carved tympanum), carved pilasters
    /// between; the projecting cornice with its row of carved beast heads and a carved timber band; the upper storey's
    /// carved red-brown timber frontage with its band of lattice and rosette windows and, at the centre, the window where
    /// the painted figures of Shiva and Parvati look out side by side; the hipped tile roof with the deep red valance hung
    /// right over that band and five gilt finials with leaf sprays on a gilt ridge plinth; a small white domed shrine at
    /// the plinth's west corner.
    /// </summary>
    internal static class ShivaParvati
    {
        public const float TopM = 11.3f;

        /// <summary>The plinth's two stages (m).</summary>
        public const double Stage1M = 1.0, PlinthM = 1.8;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double hu0 = x.PlanHu > 1 ? x.PlanHu : 7.9, hw0 = x.PlanHw > 1 ? x.PlanHw : 5.45;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu0, hw0, 1.4, 5.75, 3.3, new[] { 7.85 }, new[] { 7.0 }, new double[0], 10.0, KdsLooks.BrickPlinth, KdsLooks.BrickRed, KdsLooks.TileOld);
                return 10.0;
            }
            // Plinth: two clear stages of about a metre each (judge round 1 against shivaparvati o_01, o_03: people sit on
            // the lower one): the OSM outline to 1.0 m, the 11.8 × 7.0 m stage (w420291396) to 1.8 m; a broad front stair
            // about 4 m wide from the square to the top.
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, Stage1M, 0.25, KdsLooks.BrickPlinth, KdsLooks.Nosing, GenColliders.Brick);
            else KdsKit.Stage(x, hu0, hw0, 0, Stage1M, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0.25, GenColliders.Brick);
            const double stairHalf = 2.0;
            KdsKit.Stage(x, 5.9, 3.5, Stage1M, PlinthM, 0, 0, stairHalf, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.12, 0, GenColliders.Brick, 3.5);
            KdsKit.Stair(x, stairHalf, 3.5, Math.Max(2.2, hw0 - 3.5 + 0.7), 0, PlinthM, 0.25, 0.25, KdsLooks.Nosing, KdsLooks.BrickPlinth, 9);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 2;
                x.Stats.PlinthTopM = (float)PlinthM;
            }
            // The two stone lions stand on the lower stage at the stair's cheeks: big (about 2 m with their pedestals), dark
            // grey stone "snow lions" (shiva_parvati_window ref_02; o_09, o_10).
            int lions = m.VertexCount;
            KdsKit.LionOn(x, -(stairHalf + 0.6), 4.2, Stage1M, 0.45, 1.65, -1, false, 0.36);
            KdsKit.LionOn(x, stairHalf + 0.6, 4.2, Stage1M, 0.45, 1.65, 1, false, 0.36);
            KdsKit.Recolour(m, lions, Looks.Of(MeshColor.FromHex(0x6A665F), MaterialChannel.Stone), 0.55f);
            // A small white domed shrine at the west corner of the plinth.
            if (lod <= 1) SmallDomeShrine(x, -hu0 + 1.1, hw0 - 1.1, Stage1M);

            // Ground storey: five carved bays on the front (the centre one the door, the others lattice windows), each a
            // tall carved frame with a round-headed opening under a great semicircular carved tympanum (torana), carved
            // pilasters between (photos c_shivaparvati 02, 11; Commons 2014.12.09 Kathmandu 16).
            const double gu = 5.75, gw = 3.3, g0 = PlinthM, g1 = 4.85;
            SacredParts.BandedWall(m, x.K, gu, gw, g0, g1, KdsLooks.BrickRed, KdsLooks.Timber, lod, true, false);
            x.Solid(-gu, gu, -gw, gw, g0, g1, GenColliders.Brick);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, gu, gw), d = KdsKit.Depth(s, gu, gw);
                int bays = s % 2 == 0 ? 5 : 2;
                double pitch = 2 * a / bays;
                for (int i = 0; i < bays; i++)
                {
                    double u = -a + pitch * (i + 0.5);
                    bool door = s == 0 && i == bays / 2;
                    // The back (north) keeps plain carved lattice windows without the great toranas.
                    if (s == 2) KdsKit.BlackWindow(m, sk, u, g0 + 1.45, d, 0.95, 1.5, lod, 0.12, 3);
                    else
                    {
                        // The front bays in full; the side bays one LOD coarser.
                        int was = s == 0 ? x.Lod : x.Coarser();
                        Bay(x, sk, u, g0 + 0.12, d, Math.Min(1.45, 0.64 * pitch), 2.25, door, true);
                        x.Restore(was);
                    }
                }
                if (lod <= 1)
                {
                    // Carved pilasters between the bays.
                    for (int i = 0; i <= bays; i++)
                    {
                        double u = -a + pitch * i;
                        SacredDraw.Box(m, sk, u - 0.13, u + 0.13, g0 + 0.12, g0 + 2.45, d, d + 0.09, RedWood, BoxFaces.Wall);
                        if (lod == 0 && s == 0 && i > 0 && i < bays)
                            Shapes.Ellipsoid(m, SacredDraw.At(sk, u, g0 + 1.3, d + 0.12), Looks.Ao(RedWood, 0.9f), 0.07, 0.22, 0.05, 6, false);
                    }
                }
            }
            // Between the storeys: a brick course, the projecting cornice with its row of small carved beast heads, and the
            // carved timber band (OSM w420291386: 12.1 × 7.2 m).
            KdsKit.Cornice(x, 5.95, 3.5, g1, g1 + 0.32, 0.22, RedWood);
            if (lod == 0) CarvedBand(x, 5.95, 3.5, g1 + 0.02, 0.26);
            const double bandTop = 5.45;
            KdsKit.Cornice(x, 5.9, 3.45, g1 + 0.32, bandTop, 0.16, KdsLooks.Timber);

            // Upper storey: a carved timber frontage with the window band; the couple at the centre front.
            const double uu = 5.75, uw = 3.3, u0 = bandTop, wallTop = 7.7;
            SacredParts.BandedWall(m, x.K, uu, uw, u0, wallTop, RedWood, KdsLooks.Timber, lod, false, false);
            x.Solid(-uu, uu, -uw, uw, u0, wallTop, GenColliders.Wood);
            const double wc = 6.45;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, uu, uw), d = KdsKit.Depth(s, uu, uw);
                int n = s % 2 == 0 ? 7 : 3;
                // Carved posts framing the windows (the frontage's vertical members).
                if (lod <= 1)
                {
                    for (int i = 0; i <= n; i++)
                    {
                        double u = -a + 2 * a * i / n;
                        SacredDraw.Box(m, sk, u - 0.08, u + 0.08, u0, wallTop, d, d + 0.06, KdsLooks.Timber, BoxFaces.Wall);
                    }
                    SacredDraw.Box(m, sk, -a, a, u0 + 0.22, u0 + 0.34, d, d + 0.08, KdsLooks.Timber, BoxFaces.Wall | BoxFaces.Top);
                }
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    if (s == 0 && i == n / 2)
                    {
                        CoupleWindow(x, sk, u, u0 + 0.45, d);
                        continue;
                    }
                    int k = Math.Abs(i - n / 2);
                    if (k % 2 == 0 && lod <= 1) Rosette(x, sk, u, wc, d, 0.36);
                    else KdsKit.BlackWindow(m, sk, u, wc, d, 0.78, 0.78, lod, 0.08, 3);
                }
            }

            // The hipped roof with its ridge along the long side and the deep red valance hung right over the window
            // band (no struts show: the eave sits on the frontage's top beam), five gilt finials with leaf sprays.
            const double eu = 7.85, ew = 5.4, ev = 7.85, rv = 10.0;
            RoofSpec r = KdsKit.Roof(x, eu, ew, ev, eu - ew + 0.12, 0.12, rv, uu, uw, wallTop, KdsLooks.TileOld, 0.34);
            if (lod <= 1)
            {
                var ridge = new Path3(2);
                ridge.Add(-(eu - ew + 0.12), rv + 0.06, 0).Add(eu - ew + 0.12, rv + 0.06, 0);
                Shapes.Tube(m, x.K, Looks.Of(MeshColor.Scale(KdsLooks.TileOld.Color, 0.7f), MaterialChannel.RoofTile), ridge, 0.1, 5, true);
            }
            if (lod == 0 && x.Stats != null) x.Stats.Bells += KdsKit.Bells(x, r, 1.2, 1.0);
            double tip = Crest(x, rv);
            if (x.Stats != null)
            {
                x.Stats.Tiers = 1;
                x.Stats.Pinnacles = 3;
            }
            return tip;
        }

        /// <summary>The red-brown carved timber of the upper frontage, pilasters and toranas.</summary>
        private static readonly ShapeBrush RedWood = Looks.Of(MeshColor.FromHex(0x8A4530), MaterialChannel.WoodCarved);

        /// <summary>
        /// The gilt ridge crest (judge round 1 against shivaparvati o_03): a boat-shaped gilt base about 3.6 m long with its
        /// ends rising 0.25 m, three big gilt kalasha finials of stacked lotus bulbs (the middle one tallest, 1.3 m; the side
        /// ones 0.95 m, 1.1 m out), and four leafy gilt vine branches curling up between and beside them (S-curved stems with
        /// leaves at LOD0, bare stems at LOD1). Returns the top.
        /// </summary>
        private static double Crest(KdsCtx x, double rv)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double bv = rv + 0.02;
            // The boat: a gilt band swept along the ridge with upturned ends.
            var path = new Path3(8);
            int segs = lod == 0 ? 6 : lod == 1 ? 4 : 2;
            for (int i = 0; i <= segs; i++)
            {
                double t = -1 + 2.0 * i / segs, lift = 0.25 * Math.Pow(Math.Abs(t), 3);
                path.Add(1.8 * t, bv + 0.1 + lift, 0);
            }
            var section = new Profile2(8).SetRect(0.18, 0.2);
            Shapes.Sweep(m, x.K, KdsLooks.GiltLo, path, section, false, true, SweepFrames.Upright);
            double tip = bv + 0.2;
            for (int i = -1; i <= 1; i++)
            {
                double h = i == 0 ? 1.3 : 0.95, r = i == 0 ? 0.28 : 0.22;
                Affine3 k = x.K;
                x.K = SacredDraw.At(k, 1.1 * i, 0, 0);
                tip = Math.Max(tip, KdsKit.GiltFinial(x, bv + 0.2, h, r));
                x.K = k;
            }
            if (lod <= 1)
            {
                // Four vines: between the middle and side finials, and outside the side ones, curling outward.
                double[] us = { -1.65, -0.55, 0.55, 1.65 };
                var stem = new Path3(16);
                var ctrl = new Path3(6);
                for (int q = 0; q < 4; q++)
                {
                    double sg = us[q] < 0 ? -1 : 1, u0 = us[q];
                    ctrl.Clear().Add(u0, bv + 0.2, 0).Add(u0 + 0.12 * sg, bv + 0.45, 0).Add(u0 - 0.08 * sg, bv + 0.72, 0.03).Add(u0 + 0.1 * sg, bv + 0.95, 0)
                        .Add(u0 + 0.28 * sg, bv + 1.08, 0);
                    stem.Clear();
                    Curves.CatmullRom(ctrl, stem, lod == 0 ? 3 : 1, false);
                    Shapes.Tube(m, x.K, KdsLooks.Gilt, stem, 0.02, 4, true);
                    if (lod > 0) continue;
                    // Leaves: flat gilt blades along the stem, alternating sides.
                    for (int j = 0; j < 7; j++)
                    {
                        double t = (j + 1.0) / 8.0;
                        int at = Math.Min(stem.Count - 2, (int)(t * (stem.Count - 1)));
                        double px = stem.X[at], py = stem.Y[at], pz = stem.Z[at];
                        double side = j % 2 == 0 ? 1 : -1;
                        ShapeBrush b = j % 2 == 0 ? KdsLooks.Gilt : Looks.GiltHi;
                        double lx = px + side * 0.12, ly = py + 0.05;
                        SacredDraw.Quad(m, x.K, px, py, pz, 0.5 * (px + lx), ly + 0.03, pz + 0.02, lx, ly, pz, 0.5 * (px + lx), ly - 0.03, pz - 0.02, 0, 0, 1, b);
                        SacredDraw.Quad(m, x.K, px, py, pz, 0.5 * (px + lx), ly - 0.03, pz - 0.02, lx, ly, pz, 0.5 * (px + lx), ly + 0.03, pz + 0.02, 0, 0, -1, b);
                    }
                }
            }
            return tip;
        }

        /// <summary>
        /// One carved bay of the ground storey on side frame sk at u (frame width w, height h from v, wall plane d): the
        /// carved rectangular frame with its lintel, a round-headed opening (a dark door, or a lattice window), and over
        /// the lintel the great semicircular carved tympanum (torana) with a flame-tongued rim when <paramref name="torana"/>.
        /// </summary>
        private static void Bay(KdsCtx x, in Affine3 sk, double u, double v, double d, double w, double h, bool door, bool torana)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hw = 0.5 * w, ow = hw - 0.16, top = v + h;
            // A door is a dark opening; a window is a carved red-brown lattice over a dark ground.
            ShapeBrush fill = door ? Looks.Dark : Looks.Ao(RedWood, lod == 0 ? 0.42f : 0.6f);
            // The opening: straight jambs and a round head inside the frame.
            double spring = top - 0.16 - ow;
            SacredDraw.Panel(m, sk, u - ow, v, u + ow, spring, d + 0.012, fill);
            KdsKit.ArchPanel(m, sk, u, spring - 0.05, d + 0.012, ow, ow + 0.05, 0.004, fill, lod == 0 ? 1 : 2, false);
            // The frame: jambs, sill and a lintel with ears.
            ShapeBrush fr = RedWood;
            SacredDraw.Box(m, sk, u - hw, u - ow, v, top, d, d + 0.1, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + ow, u + hw, v, top, d, d + 0.1, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u - hw - 0.18, u + hw + 0.18, top, top + 0.18, d, d + 0.16, fr, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod <= 1)
            {
                // The spandrels over the round head and the sill.
                SacredDraw.Panel(m, sk, u - ow, spring + ow - 0.05, u + ow, top, d + 0.05, Looks.Ao(fr, 0.85f));
                SacredDraw.Box(m, sk, u - hw - 0.1, u + hw + 0.1, v - 0.06, v + 0.06, d, d + 0.14, fr, BoxFaces.Wall);
            }
            if (!door && lod == 0)
            {
                // The square lattice of the window.
                for (int i = 1; i <= 3; i++)
                {
                    double uu = u - ow + 2 * ow * i / 4;
                    SacredDraw.Panel(m, sk, uu - 0.03, v, uu + 0.03, spring + ow * 0.8, d + 0.04, RedWood);
                }
                for (int i = 1; i <= 4; i++)
                {
                    double vv = v + (spring + 0.6 * ow - v) * i / 5;
                    SacredDraw.Panel(m, sk, u - ow, vv - 0.03, u + ow, vv + 0.03, d + 0.035, RedWood);
                }
            }
            if (!torana || lod >= 2) return;
            // The great tympanum over the lintel: a carved half-disc field and its flame rim.
            double tr = hw + 0.12, tv = top + 0.18;
            KdsKit.ArchPanel(m, sk, u, tv - 0.05, d + 0.02, tr - 0.1, tr - 0.05, 0.05, Looks.Ao(fr, 0.8f), lod == 0 ? 1 : 2, false);
            KdsKit.FlameArch(m, sk, u, tv - 0.05, d + 0.04, tr - 0.12, tr - 0.07, 0.14, 0.06, fr, lod == 0 ? 1 : 2, false, false);
            if (lod == 0)
            {
                // The deity at the centre of the tympanum.
                Shapes.Ellipsoid(m, SacredDraw.At(sk, u, tv + 0.32, d + 0.1), Looks.Ao(fr, 0.95f), 0.12, 0.22, 0.06, 6, false);
            }
        }

        /// <summary>The carved band under the cornice: a row of small carved figure bosses.</summary>
        private static void CarvedBand(KdsCtx x, double hu, double hw, double v, double h)
        {
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                int n = (int)(2 * a / 0.55);
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    SacredDraw.Box(x.M, sk, u - 0.09, u + 0.09, v + 0.5 * h - 0.12, v + 0.5 * h + 0.12, d + 0.27, d + 0.37,
                                   Looks.Of(MeshColor.FromHex(0x5A3A2A), MaterialChannel.WoodCarved), BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                }
            }
        }

        /// <summary>A round carved rosette window (a dark disc in a carved ring).</summary>
        private static void Rosette(KdsCtx x, in Affine3 sk, double u, double v, double d, double r)
        {
            Affine3 f = SacredDraw.Basis(sk, u, v, d, 1, 0, 0, 0, 0, 1);
            Shapes.Cylinder(x.M, f, Looks.Recess, r, 0.02, x.Lod == 0 ? 10 : 6, 0, 0, false, true);
            Shapes.Torus(x.M, f * Affine3.Translation(0, 0.04, 0), KdsLooks.Timber, r, 0.06, x.Lod == 0 ? 10 : 6, x.Lod == 0 ? 4 : 3);
            if (x.Lod == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    double a = Math.PI * i / 4;
                    Shapes.Bar(x.M, f, KdsLooks.Timber, -r * Math.Cos(a), 0.03, -r * Math.Sin(a), r * Math.Cos(a), 0.03, r * Math.Sin(a), 0.018, 4);
                }
            }
        }

        /// <summary>The temple's signature: the central upper window, a carved opening under a cusped arch, where the painted
        /// figures of Shiva and Parvati lean out side by side, seen from the chest up: white-painted bodies and faces with
        /// black hair, eyes and brows, tall white crowns banded in gilt, gilt necklaces, their arms on the sill
        /// (shiva_parvati_window ref_03, hires c02).</summary>
        private static void CoupleWindow(KdsCtx x, in Affine3 sk, double u, double v, double d)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double w = 1.5, h = 1.05;
            // The opening: straight jambs to the spring, then a round head; the carved arch frame and the lintel band over it.
            SacredDraw.Panel(m, sk, u - 0.5 * w, v, u + 0.5 * w, v + h, d + 0.01, Looks.Recess);
            KdsKit.ArchPanel(m, sk, u, v + h - 0.05, d + 0.01, 0.5 * w, 0.42, 0.004, Looks.Recess, lod == 0 ? 1 : 2, true);
            ShapeBrush fr = KdsLooks.Timber;
            SacredDraw.Box(m, sk, u - 0.5 * w - 0.14, u - 0.5 * w, v, v + h, d, d + 0.12, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + 0.5 * w, u + 0.5 * w + 0.14, v, v + h, d, d + 0.12, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u - 0.5 * w - 0.25, u + 0.5 * w + 0.25, v - 0.08, v + 0.06, d, d + 0.22, fr, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod <= 1) KdsKit.FlameArch(m, sk, u, v + h - 0.05, d + 0.02, 0.5 * w, 0.42, 0.13, 0.1, RedWood, lod == 0 ? 1 : 2, false, true);
            if (lod >= 2) return;
            ShapeBrush body = Looks.Of(MeshColor.FromHex(0xEFE6D6), MaterialChannel.Paint), crown = Looks.Of(MeshColor.FromHex(0xF4EFE4), MaterialChannel.Paint);
            int seg = lod == 0 ? 10 : 6;
            // Shiva (left seen from the front, a little taller) and Parvati.
            for (int i = 0; i < 2; i++)
            {
                double cu = u + (i == 0 ? -0.33 : 0.33), sc = i == 0 ? 1.0 : 0.93, hv = v + 0.62 * sc;
                // Shoulders and chest over the sill, the forearms crossed on it.
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, v + 0.28 * sc, d + 0.02), body, 0.25 * sc, 0.27 * sc, 0.14, seg, false);
                if (lod == 0)
                {
                    Shapes.Capsule(m, SacredDraw.Basis(sk, cu - 0.12, v + 0.1, d + 0.05, 0.97, 0, -0.243, 0.243, 0, 0.97), body, 0.05, 0.22, 6);
                    Shapes.Capsule(m, SacredDraw.Basis(sk, cu + 0.12, v + 0.1, d + 0.05, 0.97, 0, 0.243, -0.243, 0, 0.97), body, 0.05, 0.22, 6);
                    // A gilt necklace on the chest.
                    Shapes.Torus(m, SacredDraw.Basis(sk, cu, v + 0.42 * sc, d + 0.1, 1, 0, 0, 0, 0.342, 0.94), KdsLooks.Gilt, 0.12 * sc, 0.018, 10, 3);
                }
                // Neck, head, black hair framing the face.
                Shapes.Cylinder(m, SacredDraw.At(sk, cu, v + 0.48 * sc, d + 0.05), body, 0.06, 0.08, 6, 0, 0, false, false);
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, hv, d + 0.07), body, 0.13 * sc, 0.16 * sc, 0.12, seg, false);
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, hv + 0.03, d + 0.0), KdsLooks.HairBlack, 0.145 * sc, 0.16 * sc, 0.1, seg, false);
                // The tall white crown with gilt bands, a little gilt knob on top.
                var cp = new Profile2(10);
                cp.Add(0, 0, true).Add(0.125 * sc, 0, true).Add(0.14 * sc, 0.05).Add(0.11 * sc, 0.16 * sc).Add(0.085 * sc, 0.3 * sc).Add(0.05 * sc, 0.38 * sc).Add(0, 0.41 * sc, true);
                int c0 = m.VertexCount;
                Shapes.Lathe(m, SacredDraw.At(sk, cu, hv + 0.1 * sc, d + 0.05), crown, cp, lod == 0 ? 8 : 6);
                if (lod == 0)
                {
                    Shapes.Torus(m, SacredDraw.At(sk, cu, hv + 0.13 * sc, d + 0.05), KdsLooks.Gilt, 0.135 * sc, 0.02, 8, 3);
                    Shapes.Torus(m, SacredDraw.At(sk, cu, hv + 0.3 * sc, d + 0.05), KdsLooks.Gilt, 0.09 * sc, 0.015, 8, 3);
                    Shapes.Sphere(m, SacredDraw.At(sk, cu, hv + 0.52 * sc, d + 0.05), KdsLooks.Gilt, 0.035, 5);
                    // Black eyes and brows, the red mouth (Shiva's third eye a red mark).
                    for (int e = -1; e <= 1; e += 2)
                    {
                        Shapes.Ellipsoid(m, SacredDraw.At(sk, cu + e * 0.048 * sc, hv + 0.015, d + 0.185), KdsLooks.HairBlack, 0.026, 0.014, 0.01, 4, false);
                        Shapes.Ellipsoid(m, SacredDraw.At(sk, cu + e * 0.05 * sc, hv + 0.055, d + 0.18), KdsLooks.HairBlack, 0.035, 0.008, 0.01, 4, false);
                    }
                    Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, hv - 0.075 * sc, d + 0.18), KdsLooks.PaintRed, 0.028, 0.011, 0.01, 4, false);
                    if (i == 0) Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, hv + 0.09, d + 0.185), KdsLooks.PaintRed, 0.009, 0.022, 0.008, 4, false);
                }
            }
        }

        /// <summary>A small white plastered domed shrine at (u, w) on the plinth (top v).</summary>
        private static void SmallDomeShrine(KdsCtx x, double u, double w, double v)
        {
            MeshData m = x.M;
            SacredDraw.Box(m, x.K, u - 0.55, u + 0.55, v, v + 0.9, w - 0.55, w + 0.55, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
            Shapes.Dome(m, SacredDraw.At(x.K, u, v + 0.9, w), KdsLooks.Plaster, 0.5, 0.55, x.Lod == 0 ? 12 : 8);
            Shapes.Cone(m, SacredDraw.At(x.K, u, v + 1.4, w), KdsLooks.Gilt, 0.07, 0.3, 6, 0, 0, false);
            SacredDraw.Panel(m, KdsKit.Side(x.K, 0), u - 0.2, v + 0.1, u + 0.2, v + 0.6, w + 0.56, Looks.Dark);
            x.Solid(u - 0.55, u + 0.55, w - 0.55, w + 0.55, v, v + 1.7, GenColliders.Stone);
        }
    }
}
