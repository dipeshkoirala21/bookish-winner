using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Road markings for the road-decal layer (W2_DESIGN 4.5, <c>TileLayers.RoadDecals</c>): white centre and lane
    /// lines (100 mm × 1.25, broken 1.5 / 4.5 m urban and 2 / 7 m rural, the 4 / 2 m warning pattern in the last 30 m
    /// before a junction cap, continuous on bridges), yellow edge lines (150 mm, 100 mm in from the edge), zebras
    /// (0.5 m bars with 0.5 m gaps across the carriageway, as deep as the footpath, 2-4 m) at PROP marked crossings and
    /// every 200 m on URBAN trunk, primary and secondary roads (none in old cores), stop lines 2.5 m before them, plus
    /// the junction markings of <see cref="JunctionMesher"/>. Paint follows the paint rule of
    /// <see cref="RoadWidthModel.ProfileFrom"/>. Outside URBAN areas the paint is worn (blended toward the asphalt).
    /// Decals are draped exactly like the ribbons, <see cref="DecalLiftM"/> above them, so they never z-fight. Positions
    /// are relative to the tile's south-west corner. Returns the number of pieces with markings. Thread-safe for
    /// distinct meshes.
    /// </summary>
    public static class MarkingMesher
    {
        /// <summary>Lift of decals over the surface they lie on.</summary>
        public const float DecalLiftM = 0.012f;

        public static readonly uint White = MeshColor.FromHex(0xF2F0E8);
        public static readonly uint Yellow = MeshColor.FromHex(0xF2C230);

        /// <summary>Centre and lane line width: 100 mm × 1.25.</summary>
        public const double LineWidthM = 0.125;

        public const double EdgeLineWidthM = 0.15;
        public const double ZebraSpacingM = 200.0;

        public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData decals)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (decals == null) throw new ArgumentNullException(nameof(decals));
            if (o == null) o = new RoadOptions();
            if (t.Roads.Count == 0) return 0;
            RoadLayout layout = RoadLayout.For(t);
            var g = new RoadSurface(t, h);
            int pieces = 0;
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (!RoadMesher.IsDrawn(r, o) || !RoadWidthModel.IsMotor(r.RoadClass)) continue;
                if (Lines(t, layout, ri, o, ref g, decals)) pieces++;
            }
            Crossings(t, layout, o, ref g, decals);
            JunctionMesher.Build(t, h, o, null, decals);
            return pieces;
        }

        private static uint Worn(uint paint, RoadRecord r, AreaType a)
        {
            if (a == AreaType.Urban || a == AreaType.OldCore) return paint;
            float wear = 0.3f + 0.3f * (RoadWidthModel.Hash(r.OsmWayId, 0x57454152) & 0xFF) / 255f;
            return MeshColor.Lerp(paint, RoadStyle.SurfaceRgba(r.Surface), wear);
        }

        private static bool Lines(TileData t, RoadLayout layout, int ri, RoadOptions o, ref RoadSurface g, MeshData m)
        {
            RoadRecord r = t.Roads[ri];
            RoadWidthProfile prof = layout.Profiles[ri];
            double length = prof.LengthM;
            if (length < 1) return false;
            RoadProfile p0 = layout.ProfileAt(ri, 0.5 * length);
            if (!p0.CentreLine && !p0.LaneLines && !p0.EdgeLines) return false;
            AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
            bool rural = area != AreaType.Urban && area != AreaType.OldCore && area != AreaType.PeriUrban;
            bool bridge = (r.Flags & RoadFlags.Bridge) != 0;
            float lift = RoadMesher.LiftOf(r, o) + DecalLiftM;
            uint white = Worn(White, r, area), yellow = Worn(Yellow, r, area);
            double dash = rural ? 2.0 : 1.5, gap = rural ? 7.0 : 4.5;
            bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
            const double Step = 4.0;
            // Walk the piece in short steps; each step draws the pieces of line that are painted there.
            for (double s = 0; s < length - 1e-6; s += Step)
            {
                double e = Math.Min(length, s + Step);
                if (layout.InGap(ri, 0.5 * (s + e))) continue;
                RoadProfile p = layout.ProfileAt(ri, s);
                double half = 0.5 * p.CarriagewayM, sh = p.CentreShiftM;
                if (p.EdgeLines)
                {
                    double off = half - 0.1 - 0.5 * EdgeLineWidthM;
                    Along(t, layout, ri, s, e, sh + off, EdgeLineWidthM, lift, yellow, ref g, m);
                    Along(t, layout, ri, s, e, sh - off, EdgeLineWidthM, lift, yellow, ref g, m);
                }
                bool warning = NearCap(layout, ri, s, e, 30.0);
                double d = bridge ? 1e9 : warning ? 4.0 : dash, gg = bridge ? 0 : warning ? 2.0 : gap;
                if (p.CentreLine) Dashes(t, layout, ri, s, e, sh, d, gg, lift, white, ref g, m);
                if (p.LaneLines)
                {
                    if (oneway || layout.Attrs[ri].Has(RoadAttrFlags.Dual))
                    {
                        for (int k = 1; k < p.LanesFwd; k++)
                            Dashes(t, layout, ri, s, e, sh - half + 2 * half * k / p.LanesFwd, d, gg, lift, white, ref g, m);
                    }
                    else
                    {
                        for (int k = 1; k < p.LanesFwd; k++) Dashes(t, layout, ri, s, e, sh + half * k / p.LanesFwd, d, gg, lift, white, ref g, m);
                        for (int k = 1; k < p.LanesBwd; k++) Dashes(t, layout, ri, s, e, sh - half * k / p.LanesBwd, d, gg, lift, white, ref g, m);
                    }
                }
            }
            return true;
        }

        private static bool NearCap(RoadLayout layout, int ri, double s, double e, double within)
        {
            RoadCut[] c = layout.Cuts[ri];
            if (c == null) return false;
            for (int k = 0; k < c.Length; k++)
            {
                double d = k % 2 == 0 ? c[k].S - e : s - c[k].S;
                if (d >= -1e-6 && d < within) return true;
            }
            return false;
        }

        /// <summary>The broken pattern (dash, gap) from along 0 clipped to [s, e].</summary>
        private static void Dashes(TileData t, RoadLayout layout, int ri, double s, double e, double off, double dash, double gap,
                                   double lift, uint c, ref RoadSurface g, MeshData m)
        {
            if (gap <= 0)
            {
                Along(t, layout, ri, s, e, off, LineWidthM, lift, c, ref g, m);
                return;
            }
            double period = dash + gap;
            double k0 = Math.Floor(s / period);
            for (double k = k0; k * period < e; k++)
            {
                double a = Math.Max(s, k * period), b = Math.Min(e, k * period + dash);
                if (b > a + 0.05) Along(t, layout, ri, a, b, off, LineWidthM, lift, c, ref g, m);
            }
        }

        /// <summary>A line of width <paramref name="w"/> centred on offset <paramref name="off"/> from along a to b.</summary>
        internal static void Along(TileData t, RoadLayout layout, int ri, double a, double b, double off, double w, double lift, uint c,
                                   ref RoadSurface g, MeshData m)
        {
            RoadCut ca = layout.CutAt(t, ri, a), cb = layout.CutAt(t, ri, b);
            double h = 0.5 * w;
            double ax0 = ca.CX + ca.UX * (off - h), az0 = ca.CZ + ca.UZ * (off - h), ax1 = ca.CX + ca.UX * (off + h), az1 = ca.CZ + ca.UZ * (off + h);
            double bx0 = cb.CX + cb.UX * (off - h), bz0 = cb.CZ + cb.UZ * (off - h), bx1 = cb.CX + cb.UX * (off + h), bz1 = cb.CZ + cb.UZ * (off + h);
            g.Quad(ax0, az0, bx0, bz0, bx1, bz1, ax1, az1, (float)lift, c, m);
        }

        /// <summary>A line across the road at along <paramref name="s"/>, <paramref name="depth"/> deep, from offset a to b.</summary>
        internal static void CrossLine(TileData t, RoadLayout layout, int ri, double s, double depth, double a, double b, double lift, uint c,
                                       ref RoadSurface g, MeshData m)
        {
            RoadCut c0 = layout.CutAt(t, ri, s - 0.5 * depth), c1 = layout.CutAt(t, ri, s + 0.5 * depth);
            g.Quad(c0.CX + c0.UX * a, c0.CZ + c0.UZ * a, c1.CX + c1.UX * a, c1.CZ + c1.UZ * a,
                   c1.CX + c1.UX * b, c1.CZ + c1.UZ * b, c0.CX + c0.UX * b, c0.CZ + c0.UZ * b, (float)lift, c, m);
        }

        /// <summary>A zebra from along s0 to s1: 0.5 m bars (parallel to traffic) with 0.5 m gaps across the whole
        /// carriageway.</summary>
        internal static void Zebra(TileData t, RoadLayout layout, int ri, double s0, double s1, double lift, ref RoadSurface g, MeshData m)
        {
            RoadProfile p = layout.ProfileAt(ri, 0.5 * (s0 + s1));
            double half = 0.5 * p.CarriagewayM, sh = p.CentreShiftM;
            int bars = (int)Math.Floor((2 * half - 0.2) / 1.0);
            if (bars < 1) return;
            double start = sh - 0.5 * (bars * 1.0 - 0.5);
            RoadCut c0 = layout.CutAt(t, ri, s0), c1 = layout.CutAt(t, ri, s1);
            for (int k = 0; k < bars; k++)
            {
                double a = start + k, b = a + 0.5;
                g.Quad(c0.CX + c0.UX * a, c0.CZ + c0.UZ * a, c1.CX + c1.UX * a, c1.CZ + c1.UZ * a,
                       c1.CX + c1.UX * b, c1.CZ + c1.UZ * b, c0.CX + c0.UX * b, c0.CZ + c0.UZ * b, (float)lift, White, m);
            }
        }

        /// <summary>Zebras at PROP marked crossings and every 200 m on URBAN trunk, primary and secondary roads, with
        /// stop lines 2.5 m before them for each direction of travel.</summary>
        private static void Crossings(TileData t, RoadLayout layout, RoadOptions o, ref RoadSurface g, MeshData m)
        {
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (!RoadMesher.IsDrawn(r, o) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) continue;
                AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
                if (area == AreaType.OldCore) continue; // shared streets: no zebras
                double length = layout.Profiles[ri].LengthM;
                // Real crossings from PROP.
                foreach (PropRecord pr in t.Props)
                {
                    if (pr.Kind != ObjectKind.CrossingMarked) continue;
                    double s, d;
                    if (!Project(t, layout, ri, pr.XCm / 100.0, pr.ZCm / 100.0, out s, out d) || d > 3.0) continue;
                    PlaceZebra(t, layout, ri, s, length, o, ref g, m);
                }
                bool major = r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary || r.RoadClass == RoadClass.Secondary;
                if (!major || area != AreaType.Urban) continue;
                for (double s = 0.5 * ZebraSpacingM; s < length - 20; s += ZebraSpacingM)
                {
                    if (NearCap(layout, ri, s - 1, s + 1, 30.0) || layout.InGap(ri, s)) continue;
                    PlaceZebra(t, layout, ri, s, length, o, ref g, m);
                }
            }
        }

        private static void PlaceZebra(TileData t, RoadLayout layout, int ri, double s, double length, RoadOptions o, ref RoadSurface g, MeshData m)
        {
            RoadProfile p = layout.ProfileAt(ri, s);
            double depth = Math.Max(2.0, Math.Min(4.0, Math.Max(p.FootpathLeftM, p.FootpathRightM)));
            double s0 = s - 0.5 * depth, s1 = s + 0.5 * depth;
            if (s0 < 0 || s1 > length || layout.InGap(ri, s0) || layout.InGap(ri, s1)) return;
            RoadRecord r = t.Roads[ri];
            float lift = RoadMesher.LiftOf(r, o) + DecalLiftM;
            Zebra(t, layout, ri, s0, s1, lift, ref g, m);
            double half = 0.5 * p.CarriagewayM, sh = p.CentreShiftM;
            bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
            double before = s0 - 2.5, after = s1 + 2.5;
            // Forward traffic keeps to the left half (positive offsets) and stops before s0; backward traffic after s1.
            if (before > 0) CrossLine(t, layout, ri, before, 0.2, oneway ? sh - half : sh, sh + half, lift, White, ref g, m);
            if (!oneway && after < length) CrossLine(t, layout, ri, after, 0.2, sh - half, sh, lift, White, ref g, m);
        }

        /// <summary>Along distance and distance of the nearest point of a piece's rendered centreline to (x, z).</summary>
        internal static bool Project(TileData t, RoadLayout layout, int ri, double x, double z, out double along, out double dist)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2, first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            along = 0;
            dist = double.PositiveInfinity;
            for (int k = first; k < last; k++)
            {
                double ox, oz;
                double d = RoadCorridor.PointSeg(x, z, p[2 * k] / 100.0, p[2 * k + 1] / 100.0, p[2 * k + 2] / 100.0, p[2 * k + 3] / 100.0, out ox, out oz);
                if (d < dist)
                {
                    dist = d;
                    double dx = ox - p[2 * k] / 100.0, dz = oz - p[2 * k + 1] / 100.0;
                    along = layout.AlongAt(ri, k) + Math.Sqrt(dx * dx + dz * dz);
                }
            }
            return !double.IsPositiveInfinity(dist);
        }
    }
}
