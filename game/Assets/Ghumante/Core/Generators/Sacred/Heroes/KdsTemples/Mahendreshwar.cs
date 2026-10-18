using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Mahendreshwar (Makhan Pashupati, 1561; kds-temples.md T25; DANAM KAT1510; OSM w907048006) at Makhan Tole: the walled
    /// compound (about 11.4 m square: a low white wall with an iron railing) entered on the south through the brick gateway
    /// (two tall brick piers, a lintel with a painted torana and a small gilt-edged roof, bronze lions in front); inside, the
    /// two-roof temple on its stepped platform with doors on four sides, white plastered walls with red and yellow bands,
    /// both roofs clad in dark metal sheet with gilt edges, struts and rafters painted blue, red and white, and the tall gilt
    /// pinnacle; a Nandi on the west, a trident on the north, bells. The small red shrine south-east of the compound (OSM
    /// w907048005) is drawn on its own outline.
    /// </summary>
    internal static class Mahendreshwar
    {
        public const float TopM = 11.0f;

        /// <summary>The small shrine by the compound that this structure also rebuilds.</summary>
        public const string SmallShrine = "w907048005";

        private static readonly ShapeBrush MetalRoof = Looks.Of(MeshColor.FromHex(0x4A4038), MaterialChannel.Metal);

        public static double Build(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double half = 5.7, p1 = 0.45, p2 = 0.85, wallHu = 2.3, w1 = 3.65, eave1 = 3.95, top1 = 5.85, eave2 = 7.1, top2 = 8.55;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 3.4, 3.4, p2, wallHu, wallHu, new[] { 3.95, 2.95 }, new[] { eave1, eave2 }, new[] { top1 }, 9.6, KdsLooks.StonePlinth, KdsLooks.Plaster,
                            MetalRoof);
                return 9.6;
            }
            // The stepped platform and the south stair.
            KdsKit.Stage(x, 3.4, 3.4, 0, p1, 3.15, 3.15, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.08, 0.2, GenColliders.Stone);
            KdsKit.Stage(x, 3.15, 3.15, p1, p2, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.08, 0, GenColliders.Stone);
            KdsKit.Stair(x, 0.7, 3.15, 1.25, 0, p2, 0.12, 0.25, KdsLooks.StonePlinth, KdsLooks.StonePlinth, 4);
            // The white walls with their painted bands and the four doors.
            SacredParts.BandedWall(m, x.K, wallHu, wallHu, p2, w1 + 0.3, KdsLooks.Plaster, KdsLooks.Timber, lod, true, true);
            x.Solid(-wallHu, wallHu, -wallHu, wallHu, p2, w1, GenColliders.Brick);
            if (lod <= 1)
            {
                Band(x, wallHu, p2 + 0.25, KdsLooks.PaintRed);
                Band(x, wallHu, p2 + 0.37, KdsLooks.PaintYellow);
                Band(x, wallHu, w1 - 0.45, KdsLooks.PaintYellow);
                Band(x, wallHu, w1 - 0.33, KdsLooks.PaintRed);
            }
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                KdsKit.Door(m, sk, 0, p2 + 0.08, wallHu, 0.95, 1.85, KdsLooks.TimberRed, lod, s == 0, w1 - 0.05);
                if (lod == 0 && s != 0)
                {
                    KdsKit.BlackWindow(m, sk, -1.45, p2 + 1.6, wallHu, 0.45, 0.55, lod, 0.08, 2);
                    KdsKit.BlackWindow(m, sk, 1.45, p2 + 1.6, wallHu, 0.45, 0.55, lod, 0.08, 2);
                }
            }
            // Two metal roofs with gilt edges, painted struts, the upper storey.
            RoofSpec r1 = KdsKit.Roof(x, 3.95, 3.95, eave1, 1.75, 1.75, top1, wallHu, wallHu, w1 + 0.15, MetalRoof, 0, false, 0.18, KdsLooks.Gilt, KdsLooks.Gilt);
            int st = SacredParts.Struts(m, x.K, r1, p2 + 1.3, 4, 4, false, KdsLooks.PaintBlue, 0x4D414831u, lod);
            if (lod == 0) KdsKit.Bells(x, r1, 0.9, 0.9);
            SacredParts.BandedWall(m, x.K, 1.65, 1.65, top1 - 0.1, eave2 - 0.2, KdsLooks.Plaster, KdsLooks.Timber, lod, false, true);
            x.Solid(-1.65, 1.65, -1.65, 1.65, top1 - 0.1, eave2 - 0.2, GenColliders.Brick);
            KdsPagoda.WindowBand(x, 1.65, 1.65, top1, eave2 - 0.4, 1, 0.5, 0.45);
            RoofSpec r2 = KdsKit.Roof(x, 2.95, 2.95, eave2, 0.15, 0.15, top2, 1.65, 1.65, eave2 - 0.25, MetalRoof, 0, false, 0.15, KdsLooks.Gilt, KdsLooks.Gilt);
            st += SacredParts.Struts(m, x.K, r2, top1 + 0.25, 3, 3, false, KdsLooks.PaintBlue, 0x4D414832u, lod);
            // The gilt banner from the finial down over both roofs to the south door (KAT1510 07).
            KdsKit.Pataka(x, r2, r1, 0.85, 0.08);
            if (x.Stats != null)
            {
                x.Stats.Struts += st;
                x.Stats.Tiers = 2;
            }
            double tip = KdsKit.GiltFinial(x, top2 - 0.1, TopM - top2 + 0.1, 0.22);
            // The compound: low white wall with an iron railing, the brick gateway on the south.
            const double gate = 1.25;
            // The south wall and its gateway step back from the street's corridor in front (the gate's small roof and the
            // lions must clear it).
            double ws = half;
            while (ws > 4.8 && Math.Min(x.CorridorDistance(-2.9, ws + 1.0), x.CorridorDistance(2.9, ws + 1.0)) < SacredClearance.MarginM + 0.1) ws -= 0.1;
            CompoundWall(x, -half, ws, -gate - 0.45, ws);
            CompoundWall(x, gate + 0.45, ws, half, ws);
            CompoundWall(x, half, ws, half, -half);
            CompoundWall(x, half, -half, -half, -half);
            CompoundWall(x, -half, -half, -half, ws);
            Gateway(x, ws, gate);
            // Nandi on the west (−u) facing the temple, the trident on the north, a bell by the gate.
            if (lod <= 1)
            {
                SacredParts.Pedestal(m, x.K, -4.4, 0, 0, 0.45, 0.8, 0.3, KdsLooks.StoneGrey, lod);
                SacredFigures.Nandi(m, SacredDraw.Turn(x.K, -4.4, 0.3, 0, -0.5 * Math.PI), 0, 0, 0, 1.3, KdsLooks.BronzeDark, Math.Min(2, lod + 1));
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x34312E), MaterialChannel.Metal);
                Shapes.Cylinder(m, SacredDraw.At(x.K, 0, 0, -4.5), iron, 0.04, 2.6, 5);
                Shapes.Bar(m, x.K, iron, -0.35, 2.35, -4.5, 0.35, 2.35, -4.5, 0.03, 4);
                for (int q = -1; q <= 1; q++) Shapes.Cone(m, SacredDraw.At(x.K, q * 0.35, 2.35, -4.5), iron, 0.05, q == 0 ? 0.55 : 0.4, 4, 0, 0, false);
                KdsKit.BellFrame(x, 3.6, half - 1.2, 0, 1.9, 0.38);
            }
            x.Solid(-4.85, -3.95, -0.8, 0.8, 0, 1.3, GenColliders.Stone);
            x.Cylinder(0, -4.5, 0, 2.6, 0.06);
            LittleShrine(x, SmallShrine);
            return Math.Max(tip, TopM);
        }

        private static void Band(KdsCtx x, double h, double v, in ShapeBrush b)
        {
            Mould p = SacredDraw.M;
            p.Add(0.012, v, b).Add(0.012, v + 0.1);
            SacredDraw.RectRing(x.M, x.K, 0, 0, h, h, p, false, b);
        }

        /// <summary>A run of the compound wall (white plastered, 0.85 m, iron railing to 1.55 m) between two frame points, kept
        /// out of the road corridors (its ends trimmed back).</summary>
        private static void CompoundWall(KdsCtx x, double u0, double w0, double u1, double w1)
        {
            double du = u1 - u0, dw = w1 - w0, len = Math.Sqrt(du * du + dw * dw);
            if (len < 0.3) return;
            double tu = du / len, tw = dw / len, a = 0, b = len;
            while (a < b && x.CorridorDistance(u0 + tu * a, w0 + tw * a) < SacredClearance.MarginM + 0.15) a += 0.25;
            while (b > a && x.CorridorDistance(u0 + tu * b, w0 + tw * b) < SacredClearance.MarginM + 0.15) b -= 0.25;
            if (b - a < 0.5) return;
            double su = u0 + tu * a, sw = w0 + tw * a, l = b - a;
            Affine3 seg = SacredDraw.Basis(x.K, su, 0, sw, tu, 0, tw, 0, 1, 0);
            SacredDraw.Box(x.M, seg, 0, l, -0.2, 0.85, -0.13, 0.13, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
            if (x.Lod <= 1)
            {
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x34312E), MaterialChannel.Metal);
                SacredDraw.Box(x.M, seg, 0, l, 1.5, 1.55, -0.02, 0.02, iron, BoxFaces.All);
                int n = Math.Max(2, (int)(l / (x.Lod == 0 ? 0.5 : 0.9)));
                for (int i = 0; i <= n; i++)
                {
                    double c = l * i / n;
                    SacredDraw.Box(x.M, seg, c - 0.016, c + 0.016, 0.85, 1.5, -0.012, 0.012, iron, BoxFaces.Front | BoxFaces.Back);
                }
            }
            x.Wall(su, sw, su + tu * l, sw + tw * l, 0, 1.55, 0.13);
        }

        /// <summary>The brick gateway at w = <paramref name="w"/> (clear half width <paramref name="gate"/>): two tall brick
        /// piers, a timber lintel with a painted torana, a small metal roof with gilt edges, two bronze lions outside.</summary>
        private static void Gateway(KdsCtx x, double w, double gate)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double ph = 4.4, pt = 0.45;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double u = sg * (gate + pt);
                if (lod <= 1)
                {
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, 0.5 * (ph - 0.2), w), KdsLooks.Brick, 2 * pt, ph + 0.2, 2 * pt, 0.03, 1);
                    SacredDraw.Box(m, x.K, u - pt - 0.06, u + pt + 0.06, ph - 0.25, ph, w - pt - 0.06, w + pt + 0.06, Looks.Ao(KdsLooks.Brick, 0.9f), BoxFaces.All);
                    SacredDraw.Box(m, x.K, u - pt - 0.05, u + pt + 0.05, -0.1, 0.35, w - pt - 0.05, w + pt + 0.05, KdsLooks.StonePlinth, BoxFaces.All & ~BoxFaces.Bottom);
                }
                else
                {
                    SacredDraw.Box(m, x.K, u - pt, u + pt, -0.2, ph, w - pt, w + pt, KdsLooks.Brick, BoxFaces.Wall | BoxFaces.Top);
                }
                x.Solid(u - pt, u + pt, w - pt, w + pt, 0, ph, GenColliders.Brick);
                // Bronze lions outside the gate, where they stand clear of the street.
                if (x.CorridorDistance(sg * (gate + 0.4), w + 1.5) >= SacredClearance.MarginM + 0.05)
                {
                    int first = m.VertexCount, wasL = x.Coarser();
                    KdsKit.Lion(x, sg * (gate + 0.4), w + 1.05, 0.5, 1.05, sg, false, 0.3);
                    x.Restore(wasL);
                    KdsKit.Recolour(m, first, KdsLooks.BronzeDark, 0.5f);
                }
            }
            double span = gate + 2 * pt + 0.15;
            SacredDraw.Box(m, x.K, -span, span, ph, ph + 0.35, w - 0.3, w + 0.3, KdsLooks.Timber, BoxFaces.All);
            if (lod <= 1) SacredParts.Torana(m, KdsKit.Side(x.K, 0), 0, ph - 1.05, w + 0.32, 2 * gate, Looks.Of(MeshColor.FromHex(0x2F7D8A), MaterialChannel.Paint), lod);
            KdsKit.Roof(x, span + 0.55, 0.95, ph + 0.4, span - 0.15, 0.12, ph + 1.15, span, 0.3, ph + 0.35, MetalRoof, 0, false, 0.12, KdsLooks.Gilt, KdsLooks.Gilt);
            if (lod <= 1) SacredParts.Gajur(m, SacredDraw.At(x.K, 0, 0, w), ph + 1.1, 0.9, 0.12, KdsLooks.Gilt, lod);
        }

        /// <summary>A small cube shrine (red-painted walls with a white band, a dark door, a small hipped tile roof with a
        /// valance) on another OSM outline of the tile, when present.</summary>
        internal static void LittleShrine(KdsCtx x, string osm)
        {
            ulong wr = HeritageRecord.WayRelationRef(osm);
            for (int i = 0; i < x.Tile.Buildings.Count; i++)
            {
                if (x.Tile.Buildings[i].OsmRef != wr) continue;
                int[] ring = x.Tile.Buildings[i].Rings[0];
                int n = ring.Length / 2;
                double cu = 0, cw = 0, u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
                for (int q = 0; q < n; q++)
                {
                    double u, w;
                    x.Local(ring[2 * q] / 100.0, ring[2 * q + 1] / 100.0, out u, out w);
                    cu += u / n;
                    cw += w / n;
                    u0 = Math.Min(u0, u);
                    u1 = Math.Max(u1, u);
                    w0 = Math.Min(w0, w);
                    w1 = Math.Max(w1, w);
                }
                double h = Math.Max(0.6, Math.Min(1.1, 0.5 * Math.Min(u1 - u0, w1 - w0) - 0.5));
                if (x.CorridorDistance(cu, cw) < h + SacredClearance.MarginM) return;
                KitFrame kf = x.Kf;
                Affine3 k = x.K;
                x.Kf = kf.Offset(cu, 0, cw);
                x.K = SacredDraw.Xf(x.Kf);
                int was = x.Coarser();
                KdsKit.Stage(x, h + 0.25, h + 0.25, 0, 0.3, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.06, 0.2, GenColliders.Stone);
                SacredParts.BandedWall(x.M, x.K, h, h, 0.3, 2.0, KdsLooks.BrickPainted, KdsLooks.PaintWhite, x.Lod, true, true);
                x.Solid(-h, h, -h, h, 0.3, 2.0, GenColliders.Brick);
                KdsKit.Door(x.M, KdsKit.Side(x.K, 0), 0, 0.35, h, 0.6, 1.1, KdsLooks.Timber, x.Lod, true, 1.95);
                KdsKit.Roof(x, h + 0.55, h + 0.55, 2.1, 0.1, 0.1, 2.95, h, h, 2.0, KdsLooks.TileOld, 0.22);
                if (x.Lod <= 1) KdsKit.GiltFinial(x, 2.9, 0.45, 0.07);
                x.Restore(was);
                x.Kf = kf;
                x.K = k;
                return;
            }
        }
    }
}
