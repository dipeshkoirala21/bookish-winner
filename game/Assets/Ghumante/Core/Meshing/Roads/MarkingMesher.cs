using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Road markings for the road-decal layer (W2_DESIGN 4.5, ref_roads.md §4, <c>TileLayers.RoadDecals</c>): white centre
    /// and lane lines (100 mm × 1.25, broken 1.5 / 4.5 m urban and 2 / 7 m rural, the 4 / 2 m warning pattern in the last
    /// 30 m before a junction cap, continuous on bridges), yellow edge lines (150 mm, 100 mm in from the edge), zebras
    /// (0.5 m bars with 0.5 m gaps across the carriageway, as deep as the footpath, 2-4 m) at PROP marked crossings and
    /// every 200 m on URBAN trunk, primary and secondary roads (none in old cores), stop lines 2.5 m before them, plus
    /// the junction and roundabout markings of <see cref="JunctionMesher"/>. Paint follows the paint rule of
    /// <see cref="RoadWidthModel.ProfileFrom"/>; outside URBAN areas it is worn (blended toward the asphalt).
    /// <para>
    /// Every mark follows the drawn ribbon exactly: its vertices sit on the ribbon's own rows (the same smoothed centreline
    /// points, the same carriageway columns and heights, <see cref="RibbonMesher"/>) and between them on the same linear
    /// surface, <see cref="DecalLiftM"/> above it, so paint never sinks into or floats over the smoothed road. UV0 =
    /// (<see cref="MaterialChannel.Marking"/>, 1). Positions are relative to the tile's south-west corner. Returns the
    /// number of pieces with markings. Thread-safe for distinct meshes.
    /// </para>
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

        private enum LineKind : byte
        {
            EdgeLeft,
            EdgeRight,
            Centre,
            LaneOneway,
            LaneFwd,
            LaneBwd,
        }

        private sealed class Scratch
        {
            public double[] S = new double[256];
            public double[] Off = new double[8];
        }

        [ThreadStatic] private static Scratch _scratch;

        public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData decals)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (decals == null) throw new ArgumentNullException(nameof(decals));
            if (o == null) o = new RoadOptions();
            if (t.Roads.Count == 0) return 0;
            RoadLayout layout = RoadLayout.For(t);
            RoadGrade grade = RoadGrade.For(t, h, o);
            int pieces = 0;
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (!RoadMesher.IsDrawn(t, ri, o) || !RoadWidthModel.IsMotor(r.RoadClass)) continue;
                RibbonFrames f = RibbonMesher.FramesFor(t, ri, layout, grade, o);
                if (f == null) continue;
                if (Lines(t, layout, grade, ri, f, decals)) pieces++;
                Crossings(t, layout, ri, f, decals);
            }
            JunctionMesher.Build(t, h, o, null, decals);
            return pieces;
        }

        private static uint Worn(uint paint, in RoadPaving pv, RoadRecord r, AreaType a)
        {
            if (a == AreaType.Urban || a == AreaType.OldCore) return paint;
            float wear = 0.3f + 0.3f * (RoadWidthModel.Hash(r.OsmWayId, 0x57454152) & 0xFF) / 255f;
            return MeshColor.Lerp(paint, pv.Rgba, wear);
        }

        private static bool Lines(TileData t, RoadLayout layout, RoadGrade grade, int ri, RibbonFrames f, MeshData m)
        {
            RoadRecord r = t.Roads[ri];
            RoadWidthProfile prof = layout.Profiles[ri];
            double length = prof.LengthM;
            if (length < 1) return false;
            RoadProfile p0 = layout.ProfileAt(ri, 0.5 * length);
            if (!p0.CentreLine && !p0.LaneLines && !p0.EdgeLines) return false;
            AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
            bool rural = area != AreaType.Urban && area != AreaType.OldCore && area != AreaType.PeriUrban;
            bool continuous = (r.Flags & RoadFlags.Bridge) != 0 || grade.IsAbsolute(ri);
            RoadPaving pv = layout.Paving[ri];
            uint white = Worn(White, pv, r, area), yellow = Worn(Yellow, pv, r, area);
            double dash = rural ? 2.0 : 1.5, gap = rural ? 7.0 : 4.5;
            bool oneway = (r.Flags & RoadFlags.Oneway) != 0, dual = layout.Attrs[ri].Has(RoadAttrFlags.Dual);
            // Drawn runs: between junction gaps.
            int k = 0;
            while (k + 1 < f.Count)
            {
                while (k + 1 < f.Count && f.GapAfter[k]) k++;
                if (k + 1 >= f.Count) break;
                int e = k;
                while (e + 1 < f.Count && !f.GapAfter[e]) e++;
                double a = f.S[k], b = f.S[e];
                if (b - a > 0.2)
                {
                    if (p0.EdgeLines)
                    {
                        Strip(f, layout, ri, a, b, LineKind.EdgeLeft, 0, 0, EdgeLineWidthM, yellow, m);
                        Strip(f, layout, ri, a, b, LineKind.EdgeRight, 0, 0, EdgeLineWidthM, yellow, m);
                    }
                    if (p0.CentreLine) Dashes(f, layout, ri, a, b, LineKind.Centre, 0, 0, dash, gap, continuous, white, m);
                    if (p0.LaneLines)
                    {
                        if (oneway || dual)
                        {
                            for (int q = 1; q < p0.LanesFwd; q++) Dashes(f, layout, ri, a, b, LineKind.LaneOneway, q, p0.LanesFwd, dash, gap, continuous, white, m);
                        }
                        else
                        {
                            for (int q = 1; q < p0.LanesFwd; q++) Dashes(f, layout, ri, a, b, LineKind.LaneFwd, q, p0.LanesFwd, dash, gap, continuous, white, m);
                            for (int q = 1; q < p0.LanesBwd; q++) Dashes(f, layout, ri, a, b, LineKind.LaneBwd, q, p0.LanesBwd, dash, gap, continuous, white, m);
                        }
                    }
                }
                k = e;
            }
            return true;
        }

        /// <summary>Lateral offset of a line at raw along s (follows the width profile's tapers).</summary>
        private static double LineOffset(RoadLayout layout, int ri, double s, LineKind kind, int index, int count)
        {
            RoadWidthProfile prof = layout.Profiles[ri];
            double w = prof.DrawnAt(s), half = 0.5 * w;
            double sh = layout.Attrs[ri].Has(RoadAttrFlags.Dual) ? 0.5 * (w - prof.RealM) : 0.0;
            switch (kind)
            {
                case LineKind.EdgeLeft: return sh + half - 0.1 - 0.5 * EdgeLineWidthM;
                case LineKind.EdgeRight: return sh - (half - 0.1 - 0.5 * EdgeLineWidthM);
                case LineKind.LaneOneway: return sh - half + 2 * half * index / count;
                case LineKind.LaneFwd: return sh + half * index / count;
                case LineKind.LaneBwd: return sh - half * index / count;
                default: return sh;
            }
        }

        /// <summary>Distance from raw along <paramref name="s"/> to the next junction cut ahead in either direction, if
        /// within <paramref name="within"/> (the warning pattern before a cap).</summary>
        private static bool NearCap(RoadLayout layout, int ri, double s, double within)
        {
            RoadCut[] c = layout.Cuts[ri];
            if (c == null) return false;
            for (int k = 0; k < c.Length; k++)
            {
                double d = k % 2 == 0 ? c[k].S - s : s - c[k].S;
                if (d >= -1e-6 && d < within) return true;
            }
            return false;
        }

        /// <summary>The broken pattern of a line from along 0, clipped to [a, b] (continuous on bridges).</summary>
        private static void Dashes(RibbonFrames f, RoadLayout layout, int ri, double a, double b, LineKind kind, int index, int count, double dash,
                                   double gap, bool continuous, uint c, MeshData m)
        {
            if (continuous)
            {
                Strip(f, layout, ri, a, b, kind, index, count, LineWidthM, c, m);
                return;
            }
            double s = a;
            while (s < b - 1e-6)
            {
                bool warn = NearCap(layout, ri, s, 30.0);
                double d = warn ? 4.0 : dash, g = warn ? 2.0 : gap, period = d + g;
                double k = Math.Floor(s / period);
                double d0 = k * period, d1 = d0 + d;
                double x0 = Math.Max(s, d0), x1 = Math.Min(b, d1);
                if (x1 > x0 + 0.05) Strip(f, layout, ri, x0, x1, kind, index, count, LineWidthM, c, m);
                s = Math.Max(s + 1e-3, d0 + period);
            }
        }

        /// <summary>A line of width <paramref name="w"/> along [a, b] at the offset of <paramref name="kind"/>, on the ribbon's
        /// rows.</summary>
        private static void Strip(RibbonFrames f, RoadLayout layout, int ri, double a, double b, LineKind kind, int index, int count, double w, uint c,
                                  MeshData m)
        {
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            int n = AlongSamples(f, a, b, sc);
            int prev = -1;
            float u = RoadMaterials.U(MaterialChannel.Marking);
            for (int i = 0; i < n; i++)
            {
                double s = sc.S[i];
                double off = LineOffset(layout, ri, s, kind, index, count);
                double x0, y0, z0, x1, y1, z1;
                if (!f.PointAt(s, off + 0.5 * w, out x0, out y0, out z0) || !f.PointAt(s, off - 0.5 * w, out x1, out y1, out z1))
                {
                    prev = -1;
                    continue;
                }
                int k = Math.Max(0, f.SegmentAt(s));
                int v = m.AddVertex((float)x0, (float)y0 + DecalLiftM, (float)z0, f.Nx[k], f.Ny[k], f.Nz[k], c, u, 1f);
                m.AddVertex((float)x1, (float)y1 + DecalLiftM, (float)z1, f.Nx[k], f.Ny[k], f.Nz[k], c, u, 1f);
                if (prev >= 0) RoadSweep.Quad(m, prev, v, v + 1, prev + 1);
                prev = v;
            }
        }

        /// <summary>The along samples of a strip: a, every ribbon row strictly inside (a, b), b.</summary>
        private static int AlongSamples(RibbonFrames f, double a, double b, Scratch sc)
        {
            int n = 0;
            Put(sc, n++, a);
            int k = f.SegmentAt(a);
            for (int j = Math.Max(0, k); j < f.Count; j++)
            {
                double s = f.S[j];
                if (s <= a + 1e-6) continue;
                if (s >= b - 1e-6) break;
                Put(sc, n++, s);
            }
            Put(sc, n++, b);
            return n;
        }

        private static void Put(Scratch sc, int i, double s)
        {
            if (i >= sc.S.Length) Array.Resize(ref sc.S, sc.S.Length * 2);
            sc.S[i] = s;
        }

        /// <summary>A band across the road from offset <paramref name="lo"/> to <paramref name="hi"/> between along s0 and
        /// s1 (zebra bars, stop lines), split across at the ribbon's carriageway columns inside it.</summary>
        internal static void Band(RibbonFrames f, double s0, double s1, double lo, double hi, uint c, MeshData m)
        {
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            if (hi < lo)
            {
                double tmp = lo;
                lo = hi;
                hi = tmp;
            }
            int k0 = Math.Max(0, f.SegmentAt(s0));
            int na = 0;
            sc.Off[na++] = hi;
            int b = k0 * RibbonFrames.MaxCols;
            for (int q = 0; q < f.Cols && na < sc.Off.Length - 1; q++)
            {
                double o = f.ColOff[b + q];
                if (o < hi - 0.02 && o > lo + 0.02) sc.Off[na++] = o;
            }
            sc.Off[na++] = lo;
            int n = AlongSamples(f, s0, s1, sc);
            float u = RoadMaterials.U(MaterialChannel.Marking);
            int prev = -1;
            for (int i = 0; i < n; i++)
            {
                double s = sc.S[i];
                int k = Math.Max(0, f.SegmentAt(s));
                int first = m.VertexCount;
                bool ok = true;
                for (int q = 0; q < na; q++)
                {
                    double x, y, z;
                    if (!f.PointAt(s, sc.Off[q], out x, out y, out z))
                    {
                        ok = false;
                        break;
                    }
                    m.AddVertex((float)x, (float)y + DecalLiftM, (float)z, f.Nx[k], f.Ny[k], f.Nz[k], c, u, 1f);
                }
                if (!ok)
                {
                    m.VertexCount = first;
                    prev = -1;
                    continue;
                }
                if (prev >= 0)
                    for (int q = 0; q + 1 < na; q++) RoadSweep.Quad(m, prev + q, first + q, first + q + 1, prev + q + 1);
                prev = first;
            }
        }

        /// <summary>A line across the road at along <paramref name="s"/>, <paramref name="depth"/> deep, from offset a to b.</summary>
        internal static void CrossLine(RibbonFrames f, double s, double depth, double a, double b, uint c, MeshData m)
        {
            Band(f, s - 0.5 * depth, s + 0.5 * depth, a, b, c, m);
        }

        /// <summary>A zebra from along s0 to s1: 0.5 m bars (parallel to traffic) with 0.5 m gaps across the whole
        /// carriageway.</summary>
        internal static void Zebra(RibbonFrames f, RoadLayout layout, int ri, double s0, double s1, MeshData m)
        {
            RoadProfile p = layout.ProfileAt(ri, 0.5 * (s0 + s1));
            double half = 0.5 * p.DrawnM, sh = p.DrawnShiftM;
            int bars = (int)Math.Floor((2 * half - 0.2) / 1.0);
            if (bars < 1) return;
            double start = sh - 0.5 * (bars * 1.0 - 0.5);
            for (int k = 0; k < bars; k++)
            {
                double a = start + k, b = a + 0.5;
                Band(f, s0, s1, a, b, White, m);
            }
        }

        /// <summary>Zebras at PROP marked crossings and every 200 m on URBAN trunk, primary and secondary roads, with
        /// stop lines 2.5 m before them for each direction of travel.</summary>
        private static void Crossings(TileData t, RoadLayout layout, int ri, RibbonFrames f, MeshData m)
        {
            RoadRecord r = t.Roads[ri];
            if ((r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) return;
            AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
            if (area == AreaType.OldCore && !RoadWidthModel.IsArterial(r.RoadClass)) return; // shared streets: no zebras
            double length = layout.Profiles[ri].LengthM;
            // Real crossings from PROP.
            foreach (PropRecord pr in t.Props)
            {
                if (pr.Kind != ObjectKind.CrossingMarked) continue;
                double s, d;
                if (!Project(t, layout, ri, pr.XCm / 100.0, pr.ZCm / 100.0, out s, out d) || d > 3.0) continue;
                PlaceZebra(t, layout, ri, f, s, length, m);
            }
            bool major = r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary || r.RoadClass == RoadClass.Secondary;
            if (!major || area != AreaType.Urban && area != AreaType.OldCore) return;
            for (double s = 0.5 * ZebraSpacingM; s < length - 20; s += ZebraSpacingM)
            {
                if (NearCap(layout, ri, s - 30, 61.0) || layout.InGap(ri, s)) continue;
                PlaceZebra(t, layout, ri, f, s, length, m);
            }
        }

        private static void PlaceZebra(TileData t, RoadLayout layout, int ri, RibbonFrames f, double s, double length, MeshData m)
        {
            RoadProfile p = layout.ProfileAt(ri, s);
            if (p.DrawnM < 5f) return;
            double depth = Math.Max(2.0, Math.Min(4.0, Math.Max(p.FootpathLeftM, p.FootpathRightM)));
            double s0 = s - 0.5 * depth, s1 = s + 0.5 * depth;
            if (s0 < 0 || s1 > length || layout.InGap(ri, s0) || layout.InGap(ri, s1)) return;
            RoadRecord r = t.Roads[ri];
            Zebra(f, layout, ri, s0, s1, m);
            double half = 0.5 * p.DrawnM, sh = p.DrawnShiftM;
            bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
            double before = s0 - 2.5, after = s1 + 2.5;
            // Forward traffic keeps to the left half (positive offsets) and stops before s0; backward traffic after s1.
            if (before > 0 && !layout.InGap(ri, before)) CrossLine(f, before, 0.2, oneway ? sh - half : sh, sh + half, White, m);
            if (!oneway && after < length && !layout.InGap(ri, after)) CrossLine(f, after, 0.2, sh - half, sh, White, m);
        }

        /// <summary>Along distance and distance of the nearest point of a piece's drawn (smoothed) centreline to (x, z).</summary>
        internal static bool Project(TileData t, RoadLayout layout, int ri, double x, double z, out double along, out double dist)
        {
            along = 0;
            dist = double.PositiveInfinity;
            RoadCentreline c = layout.Centres[ri];
            if (c == null) return false;
            int seg;
            double lateral;
            return c.Nearest(x, z, out seg, out along, out lateral, out dist);
        }
    }
}
