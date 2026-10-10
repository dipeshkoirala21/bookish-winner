using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Junction caps, roundabouts, road islands, police podiums and junction markings (W2_DESIGN 4.7, ref_roads.md §3)
    /// from the tile's <see cref="RoadLayout"/>. Surfaces:
    /// <list type="bullet">
    /// <item>each cap is one surface from its node to its outline (the arms' cut sections, carriageway columns included,
    /// and the kerb-return arcs between them), draped exactly a lift above the terrain, where the pinned ribbon ends meet it
    /// vertex for vertex; along every kerb return the arms' footpaths and kerbs run round the corner when both arms have one
    /// on that side, a rounded skirt otherwise;</item>
    /// <item>every true-circle roundabout (<see cref="RoadRing"/>) is a circular ring carriageway between the island edge
    /// and its outer outline with the flared entries, kerbed like the caps, with raised teardrop splitter islands on wide
    /// two-way approaches;</item>
    /// <item>islands are lathes: a mountable kerb painted in yellow and black bands, a cobbled apron for buses on
    /// roundabouts, a barrier inner kerb, a planting strip of soil and flowers and a flat grass interior at
    /// <see cref="RoadLayout.IslandInteriorHeightM"/> (the ornaments package places statues and gardens there,
    /// <see cref="RoadIsland.InteriorRadiusM"/>); police chowks get a white podium with a red umbrella only with
    /// <see cref="RoadOptions.PolicePodiums"/> (off by default: the ornaments package draws the police furniture);</item>
    /// <item>round a built-up roundabout's outer circle a kerb and paver footpath, and between two one-way approaches (the
    /// carriageways of a dual road) a kerbed nose island continuing the median up to the circle.</item>
    /// </list>
    /// Decals: zebras and stop lines on the arms of signalised and police chowks, a yellow edge line round roundabout
    /// islands, a dashed lane circle on rings at least 9 m wide, give-way dashes across every entry, and a painted ring for
    /// mini roundabouts. UV0 = (material channel, AO) everywhere.
    /// <para><see cref="RoadMesher"/> calls this with <c>decals = null</c> when <see cref="RoadOptions.JunctionCaps"/> is on,
    /// and <see cref="MarkingMesher"/> with <c>surface = null</c>, so callers normally use those two. Returns the number
    /// of caps drawn. Thread-safe for distinct meshes.</para>
    /// </summary>
    public static class JunctionMesher
    {
        /// <summary>Lift of a cap over its highest arm's ribbon.</summary>
        public const float CapExtraLiftM = 0.003f;

        /// <summary>Length of a painted kerb band (cartoon-bold 2 × the real 0.5 m).</summary>
        public const double KerbBandM = 1.0;

        private sealed class Scratch
        {
            public double[] X = new double[256], Z = new double[256];
            public float[] Ao = new float[256];
            public uint[] C = new uint[256];
            public int Count;
            public readonly double[] Cols = new double[RibbonFrames.MaxCols];
            public readonly RoadRow Side = new RoadRow();
            public readonly LatheProfile Lathe = new LatheProfile();
            public double[] OX = new double[64], OZ = new double[64], TX = new double[32], TZ = new double[32];
            public float[] OY = new float[64];

            public void Clear()
            {
                Count = 0;
            }

            public void Add(double x, double z, float ao)
            {
                Add(x, z, ao, 0u);
            }

            public void Add(double x, double z, float ao, uint c)
            {
                if (Count == X.Length)
                {
                    Array.Resize(ref X, Count * 2);
                    Array.Resize(ref Z, Count * 2);
                    Array.Resize(ref Ao, Count * 2);
                    Array.Resize(ref C, Count * 2);
                }
                X[Count] = x;
                Z[Count] = z;
                Ao[Count] = ao;
                C[Count] = c;
                Count++;
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData surface, MeshData decals)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (o == null) o = new RoadOptions();
            if (t.Roads.Count == 0) return 0;
            RoadLayout layout = RoadLayout.For(t);
            RoadGrade grade = RoadGrade.For(t, h, o);
            var g = new RoadSurface(t, h);
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            int drawn = 0;
            foreach (JunctionCap cap in layout.Caps)
            {
                if (surface != null)
                {
                    CapSurface(t, layout, cap, o, ref g, sc, surface);
                    CapCorners(t, layout, grade, cap, o, ref g, sc, surface);
                }
                if (decals != null && Controlled(cap)) ChowkMarkings(t, layout, grade, cap, o, decals);
                drawn++;
            }
            foreach (RoadRing ring in layout.Rings)
            {
                float lift = RoadMesher.LiftOf(t.Roads[ring.TopRoad], o) + CapExtraLiftM;
                if (surface != null)
                {
                    RingSurface(t, layout, ring, o, lift, ref g, sc, surface);
                    RingKerbs(t, layout, grade, ring, o, lift, ref g, sc, surface);
                    Splitters(t, layout, grade, ring, o, lift, ref g, sc, surface);
                }
                if (decals != null) RingMarkings(ring, lift, ref g, decals);
            }
            foreach (RoadIsland isl in layout.Islands)
            {
                float lift = isl.TopRoad >= 0 ? RoadMesher.LiftOf(t.Roads[isl.TopRoad], o) + CapExtraLiftM : o.LiftM;
                if (isl.Kind == IslandKind.Mini)
                {
                    if (decals != null) MiniRoundabout(t, layout, grade, isl, lift, ref g, decals);
                    continue;
                }
                if (surface == null) continue;
                Island(isl, lift, ref g, sc, surface);
                if (isl.PolicePodium && o.PolicePodiums)
                    Podium(ref g, sc, isl.X, isl.Z, lift + RoadLayout.IslandInteriorHeightM, surface);
            }
            if (surface != null && o.PolicePodiums)
            {
                // Police chowks without an island: the podium stands at the junction centre.
                foreach (JunctionCap cap in layout.Caps)
                    if ((cap.Kind == JunctionKind.Police || (cap.Flags & (byte)JunctionFlags.HasPolice) != 0) && !HasIsland(layout, cap))
                        Podium(ref g, sc, cap.X, cap.Z, RoadMesher.LiftOf(t.Roads[cap.TopRoad], o) + CapExtraLiftM, surface);
            }
            return drawn;
        }

        private static bool HasIsland(RoadLayout layout, JunctionCap cap)
        {
            foreach (RoadIsland i in layout.Islands)
                if ((i.X - cap.X) * (i.X - cap.X) + (i.Z - cap.Z) * (i.Z - cap.Z) < (i.RadiusM + 10) * (i.RadiusM + 10)) return true;
            return false;
        }

        private static bool Controlled(JunctionCap cap)
        {
            return cap.Kind == JunctionKind.Signals || cap.Kind == JunctionKind.Police ||
                   (cap.Flags & (byte)(JunctionFlags.HasPolice | JunctionFlags.HasSignals | JunctionFlags.CrossingsMarked)) != 0;
        }

        // -------------------------------------------------------------------------------------------------------
        // Caps
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Insert the carriageway columns of road <paramref name="ri"/>'s cut row between the cut's two outline
        /// points (right to left seen from the node when <paramref name="ascending"/>), at the positions, colours (the
        /// lighter crown) and AO the ribbon's end row has there.</summary>
        private static void CutInterior(TileData t, RoadLayout layout, RoadOptions o, int ri, in RoadCut cut, bool ascending, Scratch sc)
        {
            int crownCol;
            int n = RibbonMesher.CutColumns(t, ri, layout, o, cut.S, sc.Cols, out crownCol);
            RoadPaving pv = layout.Paving[ri];
            uint edge = pv.Rgba, crown = RibbonMesher.CrownRgba(pv, o);
            // Columns are left to right (descending offsets); skip the two edges (already outline points).
            if (ascending)
                for (int c = n - 2; c >= 1; c--) sc.Add(cut.CX + cut.UX * sc.Cols[c], cut.CZ + cut.UZ * sc.Cols[c], 1f, c == crownCol ? crown : edge);
            else
                for (int c = 1; c <= n - 2; c++) sc.Add(cut.CX + cut.UX * sc.Cols[c], cut.CZ + cut.UZ * sc.Cols[c], 1f, c == crownCol ? crown : edge);
        }

        /// <summary>The AO of an arm's carriageway edge at its cut, as the ribbon's end row has it: kerbed (a footpath or a
        /// median beside it) 0.72, else 0.92.</summary>
        private static float EdgeAo(RoadLayout layout, int ri, bool forward, double s, bool armLeft)
        {
            bool pieceLeft = armLeft == forward;
            if (layout.Attrs[ri].Has(RoadAttrFlags.Dual) && !pieceLeft) return 0.72f; // the median kerb
            return ArmFoot(layout, ri, forward, s, armLeft) > 0f ? 0.72f : 0.92f;
        }

        /// <summary>
        /// The cap outline with every arm's carriageway columns inserted along its cut edge (from the arm's stored cut, the
        /// very section the ribbon's end row stands on), each outline point with the colour and AO the ribbon has there:
        /// arm edges in the arm's paving and kerb AO, crown columns lighter, kerb-return points in the top road's paving
        /// with the AO of the corner's kerb.
        /// </summary>
        private static void CapOutline(TileData t, RoadLayout layout, JunctionCap cap, RoadOptions o, Scratch sc)
        {
            sc.Clear();
            int n = cap.ArmRoads.Length;
            uint topEdge = layout.Paving[cap.TopRoad].Rgba;
            int corner = -1; // the corner (between arm k and k + 1) the following return points belong to
            for (int i = 0; i < cap.Count; i++)
            {
                int armR = -1, armL = -1;
                for (int k = 0; k < n; k++)
                {
                    if (cap.ArmRightIndex[k] == i) armR = k;
                    if (cap.ArmLeftIndex[k] == i) armL = k;
                }
                if (armR >= 0 || armL >= 0)
                {
                    int arm = armR >= 0 ? armR : armL;
                    int ri = cap.ArmRoads[arm];
                    double sCut = AlongOfCut(layout, ri, cap, arm, cap.ArmForward[arm]);
                    sc.Add(cap.PolyX[i], cap.PolyZ[i], EdgeAo(layout, ri, cap.ArmForward[arm], sCut, armL >= 0), layout.Paving[ri].Rgba);
                    if (armL >= 0) corner = arm;
                    if (armR < 0) continue;
                    // Forward arm: from its right (piece-right, lowest offset) to its left; backward: piece-left first.
                    CutInterior(t, layout, o, ri, layout.StoredCut(t, ri, sCut), cap.ArmForward[arm], sc);
                    continue;
                }
                sc.Add(cap.PolyX[i], cap.PolyZ[i], corner >= 0 && CornerKerbed(layout, cap, corner) ? 0.72f : 0.92f, topEdge);
            }
        }

        /// <summary>True when corner k of a cap (between arm k and arm k + 1) gets a footpath and kerb round it.</summary>
        private static bool CornerKerbed(RoadLayout layout, JunctionCap cap, int k)
        {
            int n = cap.ArmRoads.Length, k1 = (k + 1) % n;
            int ra = cap.ArmRoads[k], rb = cap.ArmRoads[k1];
            double sa = AlongOfCut(layout, ra, cap, k, cap.ArmForward[k]), sb = AlongOfCut(layout, rb, cap, k1, cap.ArmForward[k1]);
            return ArmFoot(layout, ra, cap.ArmForward[k], sa, true) > 0f && ArmFoot(layout, rb, cap.ArmForward[k1], sb, false) > 0f;
        }

        /// <summary>The raw along value of an arm's cut.</summary>
        private static double AlongOfCut(RoadLayout layout, int ri, JunctionCap cap, int arm, bool forward)
        {
            return cap.ArmCutS[arm];
        }

        /// <summary>
        /// One cap surface: a fan from the node through a middle ring (on caps more than 8 m across) to the outline, every
        /// vertex at the terrain plus the cap lift. The outline holds the arms' cut rows vertex for vertex (positions,
        /// colours and AO), so cap and ribbons share their edges exactly; the colour blends from the arms' edges and
        /// crowns at the outline to the top road's crown at the node, so no darker rectangle or colour seam shows.
        /// </summary>
        private static void CapSurface(TileData t, RoadLayout layout, JunctionCap cap, RoadOptions o, ref RoadSurface g, Scratch sc, MeshData m)
        {
            CapOutline(t, layout, cap, o, sc);
            RoadPaving pv = layout.Paving[cap.TopRoad];
            uint crown = RibbonMesher.CrownRgba(pv, o);
            float lift = RoadMesher.LiftOf(t.Roads[cap.TopRoad], o) + CapExtraLiftM;
            float u = RoadMaterials.U(pv.Channel);
            int n = sc.Count;
            double reach = 0;
            for (int k = 0; k < n; k++) reach = Math.Max(reach, Math.Sqrt(Sq(sc.X[k] - cap.X) + Sq(sc.Z[k] - cap.Z)));
            bool mid = reach > 9.0;
            m.Reserve(2 * n + 1, 9 * n);
            int centre = Vertex(ref g, m, cap.X, cap.Z, lift, crown, u, 1f);
            int ring = m.VertexCount;
            if (mid)
                for (int k = 0; k < n; k++)
                    Vertex(ref g, m, 0.5 * (cap.X + sc.X[k]), 0.5 * (cap.Z + sc.Z[k]), lift, MeshColor.Lerp(crown, sc.C[k], 0.5f), u, 0.5f * (1f + sc.Ao[k]));
            int outer = m.VertexCount;
            for (int k = 0; k < n; k++) Vertex(ref g, m, sc.X[k], sc.Z[k], lift, sc.C[k], u, sc.Ao[k]);
            for (int k = 0; k < n; k++)
            {
                int q = k + 1 == n ? 0 : k + 1;
                if (mid)
                {
                    RoadSweep.Tri(m, centre, ring + k, ring + q, 0, 1, 0);
                    RoadSweep.Quad(m, ring + k, outer + k, outer + q, ring + q);
                }
                else
                {
                    RoadSweep.Tri(m, centre, outer + k, outer + q, 0, 1, 0);
                }
            }
        }

        private static int Vertex(ref RoadSurface g, MeshData m, double x, double z, float lift, uint c, float u, float ao)
        {
            float nx, ny, nz;
            g.Normal(x, z, out nx, out ny, out nz);
            return m.AddVertex((float)x, g.Height(x, z) + lift, (float)z, nx, ny, nz, c, u, ao);
        }

        /// <summary>The footpath width of an arm on one side at its cut (0 = none): the arm's left side seen from the node
        /// is the piece's left on a forward arm.</summary>
        private static float ArmFoot(RoadLayout layout, int ri, bool forward, double s, bool armLeft)
        {
            RoadWidthProfile p = layout.Profiles[ri];
            if (layout.Attrs[ri].Has(RoadAttrFlags.Dual) && armLeft != forward) return 0f; // the median side
            bool pieceLeft = armLeft == forward;
            float w = p.Sample(pieceLeft ? p.FootLeft : p.FootRight, s);
            return w > 0f ? Math.Max(w, RoadWidthModel.MinFootpathM) : 0f;
        }

        private static void CapCorners(TileData t, RoadLayout layout, RoadGrade grade, JunctionCap cap, RoadOptions o, ref RoadSurface g, Scratch sc,
                                       MeshData m)
        {
            int n = cap.ArmRoads.Length;
            float lift = RoadMesher.LiftOf(t.Roads[cap.TopRoad], o) + CapExtraLiftM;
            RoadPaving pv = layout.Paving[cap.TopRoad];
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                int ra = cap.ArmRoads[k], rb = cap.ArmRoads[k1];
                double sa = AlongOfCut(layout, ra, cap, k, cap.ArmForward[k]), sb = AlongOfCut(layout, rb, cap, k1, cap.ArmForward[k1]);
                RoadCut ca = layout.DrawnCutAt(t, ra, sa), cb = layout.DrawnCutAt(t, rb, sb);
                float wa = ArmFoot(layout, ra, cap.ArmForward[k], sa, true), wb = ArmFoot(layout, rb, cap.ArmForward[k1], sb, false);
                // Outward at the ends: arm k's left lateral, arm k + 1's right lateral (seen from the node).
                double sgA = cap.ArmForward[k] ? 1 : -1, sgB = cap.ArmForward[k1] ? -1 : 1;
                sc.Clear();
                for (int i = cap.ArmLeftIndex[k]; ; i = (i + 1) % cap.Count)
                {
                    // The edge vertex of each kerb row takes the colour the cap outline has there (the arm's paving at its cut
                    // edges, the top road's along the return).
                    uint c = i == cap.ArmLeftIndex[k] ? layout.Paving[ra].Rgba : i == cap.ArmRightIndex[k1] ? layout.Paving[rb].Rgba : pv.Rgba;
                    sc.Add(cap.PolyX[i], cap.PolyZ[i], 0f, c);
                    if (i == cap.ArmRightIndex[k1] || sc.Count > cap.Count) break;
                }
                RoadSweep.Side kind = wa > 0f && wb > 0f ? RoadSweep.Side.Footpath : RoadSweep.Side.Skirt;
                KerbLine(sc, ca.UX * sgA, ca.UZ * sgA, cb.UX * sgB, cb.UZ * sgB, kind, wa, wb, RoadMesher.FootpathRgba(t.Roads[ra]),
                         RoadMesher.FootpathRgba(t.Roads[rb]), pv, lift, grade, o, ref g, m);
            }
        }

        /// <summary>
        /// A kerb line along the outline points in <paramref name="sc"/> (counter-clockwise round a cap or ring, outward to
        /// the right of travel): side treatment rows at every point, outward along the polygon normal and at the two ends
        /// along the given arm laterals, joined into one strip.
        /// </summary>
        private static void KerbLine(Scratch sc, double o0x, double o0z, double o1x, double o1z, RoadSweep.Side kind, float w0, float w1, uint paver0,
                                     uint paver1, in RoadPaving pv, float lift, RoadGrade grade, RoadOptions o, ref RoadSurface g, MeshData m)
        {
            KerbLine(sc, o0x, o0z, o1x, o1z, kind, w0, w1, paver0, paver1, pv, lift, grade, o, ref g, m, false, false);
        }

        /// <summary>As the other overload, closing the raised side with an end face at the start and/or the end (a footpath
        /// that begins or stops where an approach has none).</summary>
        private static void KerbLine(Scratch sc, double o0x, double o0z, double o1x, double o1z, RoadSweep.Side kind, float w0, float w1, uint paver0,
                                     uint paver1, in RoadPaving pv, float lift, RoadGrade grade, RoadOptions o, ref RoadSurface g, MeshData m,
                                     bool closeStart, bool closeEnd)
        {
            int n = sc.Count;
            if (n < 2) return;
            double total = 0;
            for (int i = 1; i < n; i++) total += Math.Sqrt(Sq(sc.X[i] - sc.X[i - 1]) + Sq(sc.Z[i] - sc.Z[i - 1]));
            double acc = 0;
            int prev = -1, prevCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) acc += Math.Sqrt(Sq(sc.X[i] - sc.X[i - 1]) + Sq(sc.Z[i] - sc.Z[i - 1]));
                double ox, oz;
                if (i == 0)
                {
                    ox = o0x;
                    oz = o0z;
                }
                else if (i == n - 1)
                {
                    ox = o1x;
                    oz = o1z;
                }
                else
                {
                    double ax = sc.X[i] - sc.X[i - 1], az = sc.Z[i] - sc.Z[i - 1], bx = sc.X[i + 1] - sc.X[i], bz = sc.Z[i + 1] - sc.Z[i];
                    double la = Math.Sqrt(ax * ax + az * az), lb = Math.Sqrt(bx * bx + bz * bz);
                    ox = (la > 1e-9 ? az / la : 0) + (lb > 1e-9 ? bz / lb : 0);
                    oz = (la > 1e-9 ? -ax / la : 0) + (lb > 1e-9 ? -bx / lb : 0);
                }
                double ol = Math.Sqrt(ox * ox + oz * oz);
                if (ol < 1e-9) continue;
                ox /= ol;
                oz /= ol;
                double f = total > 1e-9 ? acc / total : 0;
                double w = w0 + (w1 - w0) * f;
                float ey = g.Height(sc.X[i], sc.Z[i]) + lift;
                float nx, ny, nz;
                g.Normal(sc.X[i], sc.Z[i], out nx, out ny, out nz);
                bool kerb = kind == RoadSweep.Side.Footpath;
                RoadSweep.BuildSide(sc.Side, kind, o.Detail, sc.X[i], sc.Z[i], ey, ox, oz, ox, oz, Math.Max(w, kerb ? RoadWidthModel.MinFootpathM : 0),
                                    sc.C[i] != 0u ? sc.C[i] : pv.Rgba,
                                    pv.Channel, kerb ? 0.72f : 0.92f, nx, ny, nz, f < 0.5 ? paver0 : paver1, MaterialChannel.Flagstone, grade, o.KerbHeightM,
                                    o.MedianHeightM, false);
                int cur = RoadSweep.Emit(m, sc.Side);
                if (prev >= 0 && prevCount == sc.Side.Count) RoadSweep.Join(m, prev, cur, sc.Side);
                if (i == 0 && closeStart || i == n - 1 && closeEnd)
                {
                    // Facing back along the line at the start, forward at the end.
                    int j = i == 0 ? 1 : n - 2;
                    double tx = sc.X[i] - sc.X[j], tz = sc.Z[i] - sc.Z[j];
                    RoadSweep.EndFace(m, cur, 0, sc.Side.Count - 1, tx, tz);
                }
                prev = cur;
                prevCount = sc.Side.Count;
            }
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        // -------------------------------------------------------------------------------------------------------
        // Roundabouts
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The ring carriageway: an annulus from the island edge to the outer circle (one quad per circle segment), then
        /// every entry patch from the approach's cut row (columns included, shared with the ribbon) out to the circle,
        /// fanned from the cut centre. All draped a lift above the terrain.
        /// </summary>
        private static void RingSurface(TileData t, RoadLayout layout, RoadRing ring, RoadOptions o, float lift, ref RoadSurface g, Scratch sc, MeshData m)
        {
            RoadPaving pv = layout.Paving[ring.TopRoad];
            int segs = ring.CircleSegments;
            double ri = ring.InnerRadiusM, ro = ring.OuterRadiusM;
            for (int k = 0; k < segs; k++)
            {
                double a0 = 2 * Math.PI * k / segs, a1 = 2 * Math.PI * (k + 1) / segs;
                double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                g.TriAo(ring.X + ri * c0, ring.Z + ri * s0, 0.85f, ring.X + ro * c0, ring.Z + ro * s0, 0.95f, ring.X + ro * c1, ring.Z + ro * s1, 0.95f, lift,
                        pv.Rgba, pv.Channel, m);
                g.TriAo(ring.X + ri * c0, ring.Z + ri * s0, 0.85f, ring.X + ro * c1, ring.Z + ro * s1, 0.95f, ring.X + ri * c1, ring.Z + ri * s1, 0.85f, lift,
                        pv.Rgba, pv.Channel, m);
            }
            var xs = new System.Collections.Generic.List<double>();
            var zs = new System.Collections.Generic.List<double>();
            for (int a = 0; a < ring.Arms.Length; a++)
            {
                RingArm arm = ring.Arms[a];
                xs.Clear();
                zs.Clear();
                ring.Patch(a, xs, zs);
                // The patch outline with the approach's cut columns between its first two points (right, left edge), in
                // the approach's colours and AO at the cut (as its ribbon's end row), the ring's beyond.
                sc.Clear();
                RoadPaving apv = layout.Paving[arm.Road];
                sc.Add(xs[0], zs[0], EdgeAo(layout, arm.Road, arm.AtStart, arm.Cut.S, false), apv.Rgba);
                CutInterior(t, layout, o, arm.Road, arm.Cut, arm.AtStart, sc);
                sc.Add(xs[1], zs[1], EdgeAo(layout, arm.Road, arm.AtStart, arm.Cut.S, true), apv.Rgba);
                for (int k = 2; k < xs.Count; k++) sc.Add(xs[k], zs[k], 0.95f, pv.Rgba);
                double fx = arm.Cut.CX + arm.Cut.UX * arm.Cut.Shift, fz = arm.Cut.CZ + arm.Cut.UZ * arm.Cut.Shift;
                // Fan from a point just inside the cut (not on it, so no triangle is degenerate), in the approach's crown.
                fx -= arm.DirX * 0.3;
                fz -= arm.DirZ * 0.3;
                uint fan = RibbonMesher.CrownRgba(apv, o);
                for (int k = 0; k < sc.Count; k++)
                {
                    int q = k + 1 == sc.Count ? 0 : k + 1;
                    g.TriAoC(fx, fz, 1f, fan, sc.X[k], sc.Z[k], sc.Ao[k], sc.C[k], sc.X[q], sc.Z[q], sc.Ao[q], sc.C[q], lift, pv.Channel, m);
                }
            }
        }

        /// <summary>Footpath width round a built-up roundabout's outer circle where an approach brings none (2 m real ×
        /// 1.15: Tripureshwor, Thapathali and Maitighar have kerbed paver footpaths all round).</summary>
        public const float RingFootpathM = 2.3f;

        /// <summary>Two neighbouring one-way approaches (or the two carriageways of a dual road) whose facing cut edges are at
        /// most this far apart get a kerbed island in the wedge between them instead of bare ground.</summary>
        public const double MaxNoseChordM = 8.0;

        /// <summary>
        /// The kerb lines of a ring: between each entry and the next counter-clockwise, from the left cut edge along the
        /// left flare, round the outer circle and back along the next entry's right flare. On built-up rings (and wherever
        /// an approach has one) a kerb and paver footpath runs round the circle, taking each approach's footpath width at
        /// its end (or <see cref="RingFootpathM"/>) and closed by an end face where an approach has none; a kerbed skirt
        /// elsewhere. Between two one-way approaches close together (the two carriageways of a dual road) the wedge is a
        /// raised kerbed island with a rounded nose up to the circle instead (<see cref="NoseIsland"/>).
        /// </summary>
        private static void RingKerbs(TileData t, RoadLayout layout, RoadGrade grade, RoadRing ring, RoadOptions o, float lift, ref RoadSurface g,
                                      Scratch sc, MeshData m)
        {
            int n = ring.Arms.Length;
            RoadPaving pv = layout.Paving[ring.TopRoad];
            int segs = ring.CircleSegments;
            double step = 2 * Math.PI / segs;
            AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ring.TopRoad]);
            bool builtUp = area == AreaType.Urban || area == AreaType.OldCore || area == AreaType.PeriUrban;
            for (int a = 0; a < n; a++)
            {
                RingArm aa = ring.Arms[a], bb = ring.Arms[(a + 1) % n];
                if (n > 1 && NoseIsland(t, layout, grade, ring, aa, bb, o, lift, ref g, sc, m)) continue;
                double from = aa.AngleLeft, to = bb.AngleRight;
                double sweep = RoadRing.Wrap(to - from);
                double spanA = RoadRing.Wrap(aa.AngleLeft - aa.AngleRight);
                if (n > 1 && (sweep > 2 * Math.PI - spanA - 1e-3 || RoadRing.Wrap(bb.AngleRight - aa.AngleRight) < spanA)) continue; // entries overlap
                float wa = ArmFoot(layout, aa.Road, aa.AtStart, aa.Cut.S, true), wb = ArmFoot(layout, bb.Road, bb.AtStart, bb.Cut.S, false);
                double sgA = aa.AtStart ? 1 : -1, sgB = bb.AtStart ? -1 : 1;
                sc.Clear();
                sc.Add(aa.LeftEdgeX, aa.LeftEdgeZ, 0f, layout.Paving[aa.Road].Rgba);
                for (int k = 0; k < aa.LeftX.Length; k++) sc.Add(aa.LeftX[k], aa.LeftZ[k], 0f);
                double first = Math.Ceiling(from / step) * step;
                for (double ang = first; RoadRing.Wrap(ang - from) < sweep - 1e-6; ang += step)
                {
                    if (RoadRing.Wrap(ang - from) <= 1e-6) continue;
                    sc.Add(ring.X + ring.OuterRadiusM * Math.Cos(ang), ring.Z + ring.OuterRadiusM * Math.Sin(ang), 0f);
                }
                for (int k = 0; k < bb.RightX.Length; k++) sc.Add(bb.RightX[k], bb.RightZ[k], 0f);
                sc.Add(bb.RightEdgeX, bb.RightEdgeZ, 0f, layout.Paving[bb.Road].Rgba);
                bool foot = wa > 0f || wb > 0f || builtUp;
                RoadSweep.Side kind = foot ? RoadSweep.Side.Footpath : RoadSweep.Side.Skirt;
                uint paverA = wa > 0f ? RoadMesher.FootpathRgba(t.Roads[aa.Road]) : wb > 0f ? RoadMesher.FootpathRgba(t.Roads[bb.Road]) : RoadMesher.FootpathRgba(t.Roads[ring.TopRoad]);
                uint paverB = wb > 0f ? RoadMesher.FootpathRgba(t.Roads[bb.Road]) : paverA;
                KerbLine(sc, aa.Cut.UX * sgA, aa.Cut.UZ * sgA, bb.Cut.UX * sgB, bb.Cut.UZ * sgB, kind, wa > 0f ? wa : RingFootpathM,
                         wb > 0f ? wb : RingFootpathM, paverA, paverB, pv, lift, grade, o, ref g, m, foot && wa <= 0f, foot && wb <= 0f);
            }
        }

        /// <summary>
        /// The wedge between approach <paramref name="aa"/>'s left cut edge and the next approach <paramref name="bb"/>'s
        /// right cut edge as a raised kerbed island (yellow-black banded mountable kerb, grass or concrete top), when both
        /// approaches are one-way (or dual carriageways) and their facing edges are at most <see cref="MaxNoseChordM"/> apart
        /// and roughly parallel: it continues a dual road's median (or separates a one-way pair) up to the give-way line,
        /// following both flares to the circle, or to where they meet, with a rounded nose. False (nothing drawn) otherwise.
        /// </summary>
        private static bool NoseIsland(TileData t, RoadLayout layout, RoadGrade grade, RoadRing ring, in RingArm aa, in RingArm bb, RoadOptions o, float lift,
                                       ref RoadSurface g, Scratch sc, MeshData m)
        {
            bool oneA = (t.Roads[aa.Road].Flags & RoadFlags.Oneway) != 0 || layout.Attrs[aa.Road].Has(RoadAttrFlags.Dual);
            bool oneB = (t.Roads[bb.Road].Flags & RoadFlags.Oneway) != 0 || layout.Attrs[bb.Road].Has(RoadAttrFlags.Dual);
            if (!oneA || !oneB || aa.Road == bb.Road) return false;
            double chord = Math.Sqrt(Sq(aa.LeftEdgeX - bb.RightEdgeX) + Sq(aa.LeftEdgeZ - bb.RightEdgeZ));
            if (chord > MaxNoseChordM || chord < 0.3) return false;
            if (aa.DirX * bb.DirX + aa.DirZ * bb.DirZ < Math.Cos(45.0 * Math.PI / 180.0)) return false;
            // A: from aa's left edge inward along its left flare; B: from bb's right edge inward along its right flare.
            int na = aa.LeftX.Length + 1, nb = bb.RightX.Length + 1;
            double[] ax = new double[na], az = new double[na], bx = new double[nb], bz = new double[nb];
            ax[0] = aa.LeftEdgeX;
            az[0] = aa.LeftEdgeZ;
            for (int k = 0; k < aa.LeftX.Length; k++)
            {
                ax[k + 1] = aa.LeftX[k];
                az[k + 1] = aa.LeftZ[k];
            }
            bx[0] = bb.RightEdgeX;
            bz[0] = bb.RightEdgeZ;
            for (int k = 0; k < bb.RightX.Length; k++)
            {
                bx[k + 1] = bb.RightX[bb.RightX.Length - 1 - k];
                bz[k + 1] = bb.RightZ[bb.RightX.Length - 1 - k];
            }
            int hitA = -1, hitB = -1;
            double px = 0, pz = 0;
            for (int i = 0; i + 1 < na && hitA < 0; i++)
            {
                for (int j = 0; j + 1 < nb; j++)
                {
                    double ix, iz;
                    if (!SegmentsCross(ax[i], az[i], ax[i + 1], az[i + 1], bx[j], bz[j], bx[j + 1], bz[j + 1], out ix, out iz)) continue;
                    hitA = i;
                    hitB = j;
                    px = ix;
                    pz = iz;
                    break;
                }
            }
            // Beyond the cut, between two single carriageways without a footpath on the facing sides, the island runs on
            // between their edges as long as they stay close (a dual road's medians are drawn by its ribbons).
            int tail = 0;
            bool dualPair = layout.Attrs[aa.Road].Has(RoadAttrFlags.Dual) && layout.Attrs[bb.Road].Has(RoadAttrFlags.Dual);
            if (!dualPair && ArmFoot(layout, aa.Road, aa.AtStart, aa.Cut.S, true) <= 0f && ArmFoot(layout, bb.Road, bb.AtStart, bb.Cut.S, false) <= 0f)
            {
                for (int k = 1; k * NoseTailStepM <= NoseTailM; k++)
                {
                    double d = k * NoseTailStepM;
                    double sa = aa.Cut.S + (aa.AtStart ? d : -d), sb = bb.Cut.S + (bb.AtStart ? d : -d);
                    if (sa < 0 || sa > layout.Profiles[aa.Road].LengthM || sb < 0 || sb > layout.Profiles[bb.Road].LengthM) break;
                    if (layout.InGap(aa.Road, sa) || layout.InGap(bb.Road, sb)) break;
                    double tax, taz, tbx, tbz;
                    ArmEdge(t, layout, aa, sa, true, out tax, out taz);
                    ArmEdge(t, layout, bb, sb, false, out tbx, out tbz);
                    if (Math.Sqrt(Sq(tax - tbx) + Sq(taz - tbz)) > MaxNoseChordM) break;
                    if (sc.TX.Length < 2 * k) Array.Resize(ref sc.TX, 4 * k);
                    if (sc.TZ.Length < 2 * k) Array.Resize(ref sc.TZ, 4 * k);
                    sc.TX[2 * k - 2] = tax;
                    sc.TZ[2 * k - 2] = taz;
                    sc.TX[2 * k - 1] = tbx;
                    sc.TZ[2 * k - 1] = tbz;
                    tail = k;
                }
            }
            int c = 0;
            for (int k = tail; k >= 1; k--) Put(sc, c++, sc.TX[2 * k - 2], sc.TZ[2 * k - 2]);
            if (hitA >= 0)
            {
                // The flares meet: A up to the meeting point, a rounded nose, B back out.
                for (int i = 0; i <= hitA; i++) Put(sc, c++, ax[i], az[i]);
                double qax, qaz, qbx, qbz;
                Back(px, pz, ax[hitA], az[hitA], 0.8, out qax, out qaz);
                Back(px, pz, bx[hitB], bz[hitB], 0.8, out qbx, out qbz);
                Put(sc, c++, qax, qaz);
                for (int q = 1; q <= 3; q++)
                {
                    double u = q / 4.0, v = 1 - u;
                    Put(sc, c++, v * v * qax + 2 * u * v * px + u * u * qbx, v * v * qaz + 2 * u * v * pz + u * u * qbz);
                }
                Put(sc, c++, qbx, qbz);
                for (int j = hitB; j >= 0; j--) Put(sc, c++, bx[j], bz[j]);
            }
            else
            {
                // Both flares reach the circle: the island runs round it between them (outside the ring carriageway).
                for (int i = 0; i < na; i++) Put(sc, c++, ax[i], az[i]);
                double step = 2 * Math.PI / ring.CircleSegments;
                double from = aa.AngleLeft, sweep = RoadRing.Wrap(bb.AngleRight - from);
                if (sweep > Math.PI) return false;
                for (double ang = Math.Ceiling(from / step) * step; RoadRing.Wrap(ang - from) < sweep - 1e-6; ang += step)
                {
                    if (RoadRing.Wrap(ang - from) <= 1e-6) continue;
                    Put(sc, c++, ring.X + ring.OuterRadiusM * Math.Cos(ang), ring.Z + ring.OuterRadiusM * Math.Sin(ang));
                }
                for (int j = nb - 1; j >= 0; j--) Put(sc, c++, bx[j], bz[j]);
            }
            for (int k = 1; k <= tail; k++) Put(sc, c++, sc.TX[2 * k - 1], sc.TZ[2 * k - 1]);
            double area = Area(sc.OX, sc.OZ, c);
            if (Math.Abs(area) < 1.0) return false;
            if (area < 0) Reverse(sc, c);
            double size = t.Tile.Size;
            double mx = 0, mz = 0;
            for (int i = 0; i < c; i++)
            {
                if (sc.OX[i] < 0.05 || sc.OZ[i] < 0.05 || sc.OX[i] > size - 0.05 || sc.OZ[i] > size - 0.05) return false;
                // On the ring and its entries the surface is the terrain plus the ring lift; along the tail, the approach
                // ribbons' drawn surface (never under the terrain plus the lift).
                sc.OY[i] = Math.Max(g.Height(sc.OX[i], sc.OZ[i]) + lift, TailSurface(layout, grade, aa, bb, sc.OX[i], sc.OZ[i]));
                mx += sc.OX[i];
                mz += sc.OZ[i];
            }
            mx /= c;
            mz /= c;
            float height = o.MedianHeightM + 0.02f;
            float raise = IslandRaise(ref g, sc, c, lift, height);
            bool grass = Math.Abs(area) >= 8.0;
            RoadKit.KerbedOutline(m, sc.OX, sc.OZ, sc.OY, c, g.Height(mx, mz) + lift, height + raise, KerbBandM, RoadMaterials.KerbYellow,
                                  RoadMaterials.KerbBlack, grass ? RoadStyle.IslandGrass : RoadMaterials.MedianConcrete,
                                  grass ? MaterialChannel.Grass : MaterialChannel.Concrete);
            return true;
        }

        /// <summary>A nose island's top stays at least this above the lifted terrain between its two carriageways.</summary>
        public const float IslandGroundClearM = 0.08f;

        /// <summary>... rising by at most this above its kerb height to do so.</summary>
        public const float MaxIslandRaiseM = 0.25f;

        /// <summary>Spacing of the ground samples inside a nose island.</summary>
        public const double IslandSampleM = 0.5;

        /// <summary>
        /// How far a nose island's top (outline <c>sc.OX/OZ/OY</c>, <paramref name="c"/> points, plus
        /// <paramref name="height"/>) must rise so the lifted terrain inside it stays <see cref="IslandGroundClearM"/>
        /// below: the outline stands on the two carriageways' edges and the top is planar between them, so a low ridge of
        /// ground between two carriageways graded apart would show through. The top over a sample is estimated by the
        /// inverse-distance mean of the outline heights (the ear-clipped top lies between them; the clearance covers the
        /// difference). At most <see cref="MaxIslandRaiseM"/>; no allocation.
        /// </summary>
        private static float IslandRaise(ref RoadSurface g, Scratch sc, int c, float lift, float height)
        {
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            for (int i = 0; i < c; i++)
            {
                x0 = Math.Min(x0, sc.OX[i]);
                z0 = Math.Min(z0, sc.OZ[i]);
                x1 = Math.Max(x1, sc.OX[i]);
                z1 = Math.Max(z1, sc.OZ[i]);
            }
            float raise = 0f;
            for (double z = z0 + 0.5 * IslandSampleM; z < z1; z += IslandSampleM)
            {
                for (double x = x0 + 0.5 * IslandSampleM; x < x1; x += IslandSampleM)
                {
                    bool inside = false;
                    for (int i = 0, j = c - 1; i < c; j = i++)
                        if ((sc.OZ[i] > z) != (sc.OZ[j] > z) && x < sc.OX[j] + (sc.OX[i] - sc.OX[j]) * (z - sc.OZ[j]) / (sc.OZ[i] - sc.OZ[j]))
                            inside = !inside;
                    if (!inside) continue;
                    double w = 0, wy = 0;
                    for (int i = 0; i < c; i++)
                    {
                        double d2 = Sq(sc.OX[i] - x) + Sq(sc.OZ[i] - z) + 1e-6, wi = 1.0 / (d2 * d2);
                        w += wi;
                        wy += wi * sc.OY[i];
                    }
                    float under = g.Height(x, z) + lift + IslandGroundClearM - ((float)(wy / w) + height);
                    if (under > raise) raise = under;
                }
            }
            return Math.Min(raise, MaxIslandRaiseM);
        }

        /// <summary>Spacing and longest reach of a nose island's tail beyond the cut.</summary>
        public const double NoseTailStepM = 2.0, NoseTailM = 14.0;

        /// <summary>The carriageway edge of a ring approach on its arm-left (counter-clockwise side seen from the ring) or
        /// arm-right at raw along <paramref name="s"/>.</summary>
        private static void ArmEdge(TileData t, RoadLayout layout, in RingArm arm, double s, bool armLeft, out double x, out double z)
        {
            RoadCut c = layout.DrawnCutAt(t, arm.Road, s);
            bool pieceLeft = armLeft == arm.AtStart;
            double off = pieceLeft ? c.Shift + c.Half : c.Shift - c.Half;
            x = c.CX + c.UX * off;
            z = c.CZ + c.UZ * off;
        }

        /// <summary>The higher of the two approaches' drawn surfaces at a nose island point beyond their cuts (−∞ inside the
        /// ring entry).</summary>
        private static float TailSurface(RoadLayout layout, RoadGrade grade, in RingArm aa, in RingArm bb, double x, double z)
        {
            float best = float.NegativeInfinity;
            for (int q = 0; q < 2; q++)
            {
                RingArm arm = q == 0 ? aa : bb;
                double dx = x - arm.Cut.CX, dz = z - arm.Cut.CZ;
                double beyond = dx * arm.DirX + dz * arm.DirZ;
                if (beyond <= 0.05) continue;
                float y;
                if (grade.TrySurfaceAt(arm.Road, x, z, 60.0, out y)) best = Math.Max(best, y);
            }
            return best;
        }

        /// <summary>The point <paramref name="d"/> back from (px, pz) toward (qx, qz) (at most half way).</summary>
        private static void Back(double px, double pz, double qx, double qz, double d, out double x, out double z)
        {
            double l = Math.Sqrt(Sq(qx - px) + Sq(qz - pz));
            double f = l > 1e-9 ? Math.Min(d, 0.5 * l) / l : 0;
            x = px + (qx - px) * f;
            z = pz + (qz - pz) * f;
        }

        /// <summary>True when segments a0-a1 and b0-b1 cross (proper intersection), with the crossing point.</summary>
        private static bool SegmentsCross(double a0x, double a0z, double a1x, double a1z, double b0x, double b0z, double b1x, double b1z, out double x,
                                          out double z)
        {
            x = z = 0;
            double rx = a1x - a0x, rz = a1z - a0z, sx = b1x - b0x, sz = b1z - b0z;
            double den = rx * sz - rz * sx;
            if (Math.Abs(den) < 1e-12) return false;
            double qx = b0x - a0x, qz = b0z - a0z;
            double tt = (qx * sz - qz * sx) / den, uu = (qx * rz - qz * rx) / den;
            if (tt <= 1e-6 || tt > 1 || uu <= 1e-6 || uu > 1) return false;
            x = a0x + rx * tt;
            z = a0z + rz * tt;
            return true;
        }

        /// <summary>
        /// Raised teardrop splitter islands on the wide two-way approaches: from just outside the circle along the arm's
        /// axis, <see cref="RingArm.SplitterWidthM"/> wide with a round nose toward the ring, tapering to a rounded tip; it
        /// stands on the ring surface inside the cut and on the approach ribbon's drawn surface beyond it.
        /// </summary>
        private static void Splitters(TileData t, RoadLayout layout, RoadGrade grade, RoadRing ring, RoadOptions o, float lift, ref RoadSurface g, Scratch sc,
                                      MeshData m)
        {
            foreach (RingArm arm in ring.Arms)
            {
                if (!arm.Splitter) continue;
                double dx = arm.DirX, dz = arm.DirZ;
                double lx = -dz, lz = dx;
                // Axis through the cut's carriageway centre; start where it is 0.4 m outside the outer circle.
                double cx = arm.Cut.CX + arm.Cut.UX * arm.Cut.Shift, cz = arm.Cut.CZ + arm.Cut.UZ * arm.Cut.Shift;
                double fx = cx - ring.X, fz = cz - ring.Z;
                double b = fx * dx + fz * dz, c = fx * fx + fz * fz - Sq(ring.OuterRadiusM + 0.4);
                double disc = b * b - c;
                if (disc < 0) continue;
                double u0 = -b + Math.Sqrt(disc); // along +dir from the cut centre (negative: toward the ring)
                double ax = cx + dx * u0, az = cz + dz * u0;
                double len = arm.SplitterLengthM, hw = 0.5 * arm.SplitterWidthM;
                const int Nose = 7, Side = 5;
                int n = 0;
                // Nose semicircle (toward the ring), then the right side to the tip, then back along the left side.
                for (int q = 0; q <= Nose; q++)
                {
                    double ang = Math.PI * 0.5 + Math.PI * q / Nose; // left (+lat) round the front (−dir) to right (−lat)
                    double u = hw + hw * Math.Cos(ang), v = hw * Math.Sin(ang);
                    Put(sc, n++, ax + dx * u + lx * v, az + dz * u + lz * v);
                }
                for (int q = 1; q <= Side; q++)
                {
                    double f = (double)q / Side, u = hw + (len - hw - 0.3) * f, v = -(hw - (hw - 0.25) * f * f);
                    Put(sc, n++, ax + dx * u + lx * v, az + dz * u + lz * v);
                }
                Put(sc, n++, ax + dx * len, az + dz * len);
                for (int q = Side; q >= 1; q--)
                {
                    double f = (double)q / Side, u = hw + (len - hw - 0.3) * f, v = hw - (hw - 0.25) * f * f;
                    Put(sc, n++, ax + dx * u + lx * v, az + dz * u + lz * v);
                }
                if (Area(sc.OX, sc.OZ, n) < 0) Reverse(sc, n);
                for (int i = 0; i < n; i++) sc.OY[i] = SurfaceNear(layout, grade, ring, arm, sc.OX[i], sc.OZ[i], lift, ref g);
                double mx = ax + dx * 0.5 * len, mz = az + dz * 0.5 * len;
                float my = SurfaceNear(layout, grade, ring, arm, mx, mz, lift, ref g);
                RoadKit.KerbedOutline(m, sc.OX, sc.OZ, sc.OY, n, my, o.MedianHeightM + 0.02f, KerbBandM, RoadMaterials.KerbYellow, RoadMaterials.KerbBlack,
                                      RoadStyle.IslandGrass, MaterialChannel.Grass);
            }
        }

        private static void Put(Scratch sc, int i, double x, double z)
        {
            if (i >= sc.OX.Length)
            {
                Array.Resize(ref sc.OX, 2 * sc.OX.Length);
                Array.Resize(ref sc.OZ, 2 * sc.OZ.Length);
                Array.Resize(ref sc.OY, 2 * sc.OY.Length);
            }
            sc.OX[i] = x;
            sc.OZ[i] = z;
        }

        private static double Area(double[] x, double[] z, int n)
        {
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) a += x[j] * z[i] - x[i] * z[j];
            return 0.5 * a;
        }

        private static void Reverse(Scratch sc, int n)
        {
            Array.Reverse(sc.OX, 0, n);
            Array.Reverse(sc.OZ, 0, n);
        }

        /// <summary>The drawn surface under a splitter point: the ring surface (terrain + lift) inside the ring entry, the
        /// approach ribbon's surface beyond the cut.</summary>
        private static float SurfaceNear(RoadLayout layout, RoadGrade grade, RoadRing ring, RingArm arm, double x, double z, float lift, ref RoadSurface g)
        {
            double dx = x - (arm.Cut.CX), dz = z - (arm.Cut.CZ);
            double beyond = dx * arm.DirX + dz * arm.DirZ; // > 0: on the approach side of the cut
            if (beyond <= 0.05) return g.Height(x, z) + lift;
            double s = arm.AtStart ? arm.Cut.S + beyond : arm.Cut.S - beyond;
            double lat = dx * arm.Cut.UX + dz * arm.Cut.UZ;
            return grade.SurfaceY(arm.Road, s, lat, x, z);
        }

        private static void RingMarkings(RoadRing ring, float lift, ref RoadSurface g, MeshData decals)
        {
            float dl = lift + MarkingMesher.DecalLiftM;
            double ri = ring.InnerRadiusM;
            int segs = RoadKit.Segments(ri + 0.4, 1.5, 24, 160);
            // Yellow edge line round the island.
            g.Arc(ring.X, ring.Z, ri + 0.3, ri + 0.3 + MarkingMesher.EdgeLineWidthM, 0, 2 * Math.PI, segs, dl, MarkingMesher.Yellow, MaterialChannel.Marking, 1f,
                  decals);
            // A dashed lane circle on wide rings (two circulating lanes).
            if (ring.WidthM >= 9f)
            {
                double rm = 0.5 * (ri + ring.OuterRadiusM);
                double circ = 2 * Math.PI * rm;
                int dashes = Math.Max(8, (int)Math.Round(circ / 6.0));
                for (int k = 0; k < dashes; k++)
                {
                    double a0 = 2 * Math.PI * k / dashes, a1 = a0 + 2 * Math.PI * 1.5 / circ;
                    g.Arc(ring.X, ring.Z, rm - 0.5 * MarkingMesher.LineWidthM, rm + 0.5 * MarkingMesher.LineWidthM, a0, a1, 2, dl, MarkingMesher.White,
                          MaterialChannel.Marking, 1f, decals);
                }
            }
            // Give-way dashes across every entry, just inside the outer circle.
            for (int a = 0; a < ring.Arms.Length; a++)
            {
                if (!ring.Arms[a].Entry) continue;
                double start = ring.Arms[a].AngleRight, span = RoadRing.Wrap(ring.Arms[a].AngleLeft - ring.Arms[a].AngleRight);
                if (span > Math.PI) continue;
                double r1 = ring.OuterRadiusM - 0.3, r0 = r1 - 0.3;
                double arc = span * r1;
                int dashes = Math.Max(2, (int)Math.Floor(arc / 1.0));
                for (int k = 0; k < dashes; k++)
                {
                    double a0 = start + span * (k + 0.2) / dashes, a1 = start + span * (k + 0.8) / dashes;
                    g.Arc(ring.X, ring.Z, r0, r1, a0, a1, 1, dl, MarkingMesher.White, MaterialChannel.Marking, 1f, decals);
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Islands and furniture
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A raised island as lathes on the surface: the mountable outer kerb (−0.04 m at the island edge up to
        /// <see cref="RoadOptions.MedianHeightM"/>, a rounded crest) in yellow and black bands, then the cobbled apron, a
        /// barrier inner kerb, the planting strip (soil, a ridge of flowers) and the grass interior at
        /// <see cref="RoadLayout.IslandInteriorHeightM"/>.
        /// </summary>
        private static void Island(in RoadIsland isl, float lift, ref RoadSurface g, Scratch sc, MeshData m)
        {
            double r = isl.RadiusM;
            if (r < 0.6) return;
            LatheProfile p = sc.Lathe;
            int segs = RoadKit.Segments(r, 1.4, 24, 128);
            int bands = RoadKit.Segments(r, KerbBandM, 16, 200);
            if ((bands & 1) != 0) bands++;
            const float KerbH = 0.12f;
            // Painted outer kerb.
            p.Clear();
            p.Add(r + 0.03, -0.04, 0.7, 0.7, 0, MaterialChannel.Paint, 0.7f);
            p.Add(r - 0.10, KerbH - 0.015, 0.4, 0.92, 0, MaterialChannel.Paint, 1f);
            p.Add(r - 0.22, KerbH, 0.05, 1.0, 0, MaterialChannel.Paint, 1f);
            RoadKit.KerbBands(m, ref g, isl.X, isl.Z, bands, lift, p, RoadMaterials.KerbYellow, RoadMaterials.KerbBlack, MaterialChannel.Paint);
            // Apron, inner kerb, planting strip, grass.
            float interior = RoadLayout.IslandInteriorHeightM;
            double edge = r - 0.22;
            p.Clear();
            double apron = Math.Min(isl.ApronM, 0.45 * r);
            double inner = edge;
            if (apron > 0.3)
            {
                p.Add(edge, KerbH, 0, 1, RoadMaterials.Apron, MaterialChannel.Flagstone, 0.85f);
                inner = r - apron;
                p.Add(inner, KerbH + 0.02, 0, 1, RoadMaterials.Apron, MaterialChannel.Flagstone, 0.8f, true);
            }
            else
            {
                p.Add(edge, KerbH, 0, 1, RoadMaterials.Kerb, MaterialChannel.Concrete, 0.9f, true);
            }
            // Barrier inner kerb with a rounded nose.
            float kerbBase = apron > 0.3 ? KerbH + 0.02f : KerbH;
            p.Add(inner, kerbBase, 1, 0.05, RoadMaterials.Kerb, MaterialChannel.Concrete, 0.6f);
            p.Add(inner - 0.03, interior - 0.03, 0.7, 0.7, RoadMaterials.Kerb, MaterialChannel.Concrete, 0.95f);
            double kerbTop = inner - RoadWidthModel.KerbTopM;
            p.Add(kerbTop, interior, 0, 1, RoadMaterials.Kerb, MaterialChannel.Concrete, 1f, true);
            double plant = isl.PlantingM;
            double grassFrom = kerbTop;
            if (plant > 0.2 && kerbTop - plant > 0.3)
            {
                p.Add(kerbTop, interior, 0, 1, RoadMaterials.Soil, MaterialChannel.Dirt, 0.8f);
                p.Add(kerbTop - 0.5 * plant, interior + 0.12, 0.2, 1, RoadMaterials.Flowers, MaterialChannel.Foliage, 0.95f);
                grassFrom = kerbTop - plant;
                p.Add(grassFrom, interior + 0.02, 0, 1, RoadMaterials.Soil, MaterialChannel.Dirt, 0.85f, true);
            }
            if (grassFrom > 0.05)
            {
                p.Add(grassFrom, interior + 0.02, 0, 1, RoadStyle.IslandGrass, MaterialChannel.Grass, 0.9f);
                p.Add(0, interior + 0.02, 0, 1, RoadStyle.IslandGrass, MaterialChannel.Grass, 1f);
            }
            RoadKit.Lathe(m, ref g, isl.X, isl.Z, segs, lift, p, false);
        }

        /// <summary>Police podium (W2_DESIGN 4.7): Ø 1.4 m, 0.35 m high with a rounded top edge, white, a pole and a 2.2 m
        /// red umbrella.</summary>
        private static void Podium(ref RoadSurface g, Scratch sc, double x, double z, float baseLift, MeshData m)
        {
            LatheProfile p = sc.Lathe;
            uint white = RoadStyle.Podium;
            p.Clear();
            p.Add(0.72, -0.12, 1, 0, white, MaterialChannel.Plaster, 0.6f);
            p.Add(0.72, 0.28, 1, 0.1, white, MaterialChannel.Plaster, 0.95f);
            p.Add(0.66, 0.35, 0.6, 0.8, white, MaterialChannel.Plaster, 1f);
            p.Add(0.55, 0.37, 0.1, 1, white, MaterialChannel.Plaster, 1f);
            p.Add(0, 0.37, 0, 1, white, MaterialChannel.Plaster, 1f);
            RoadKit.Lathe(m, ref g, x, z, 16, baseLift, p, true);
            float y = g.Height(x, z) + baseLift;
            RoadKit.Pole(m, x, z, 0.04, y + 0.37f, y + 2.25f, 6, MeshColor.FromHex(0x5A5A5A), MaterialChannel.Metal);
            uint red = MeshColor.FromHex(0xC9433A);
            p.Clear();
            p.Add(1.15, 1.98, 0.5, -0.85, red, MaterialChannel.Fabric, 0.7f);
            p.Add(1.15, 2.02, 0.45, 0.9, red, MaterialChannel.Fabric, 1f);
            p.Add(0.6, 2.28, 0.4, 0.92, red, MaterialChannel.Fabric, 1f);
            p.Add(0, 2.42, 0, 1, red, MaterialChannel.Fabric, 1f);
            RoadKit.Lathe(m, ref g, x, z, 12, baseLift, p, true);
        }

        /// <summary>A painted mini roundabout: a white ring and a white dot on the drawn surface (draped on a cap, else on
        /// the top road's ribbon).</summary>
        private static void MiniRoundabout(TileData t, RoadLayout layout, RoadGrade grade, in RoadIsland isl, float lift, ref RoadSurface g, MeshData decals)
        {
            bool onCap = false;
            foreach (JunctionCap cap in layout.Caps)
                if (Sq(cap.X - isl.X) + Sq(cap.Z - isl.Z) < 1.0) onCap = true;
            float dl = lift + MarkingMesher.DecalLiftM;
            if (onCap || isl.TopRoad < 0 || !grade.Has(isl.TopRoad))
            {
                g.Disc(isl.X, isl.Z, Math.Max(0, isl.RadiusM - 0.3), isl.RadiusM, 20, dl, MarkingMesher.White, MaterialChannel.Marking, 1f, decals);
                g.Disc(isl.X, isl.Z, 0, 0.6, 10, dl, MarkingMesher.White, MaterialChannel.Marking, 1f, decals);
                return;
            }
            // On a ribbon: flat rings at the ribbon's drawn height.
            float y;
            if (!grade.TrySurfaceAt(isl.TopRoad, isl.X, isl.Z, 30, out y)) y = g.Height(isl.X, isl.Z) + lift;
            y += MarkingMesher.DecalLiftM + 0.01f;
            float u = RoadMaterials.U(MaterialChannel.Marking);
            const int N = 20;
            double r0 = Math.Max(0, isl.RadiusM - 0.3), r1 = isl.RadiusM;
            int first = decals.VertexCount;
            for (int k = 0; k < N; k++)
            {
                double a = 2 * Math.PI * k / N;
                decals.AddVertex((float)(isl.X + r0 * Math.Cos(a)), y, (float)(isl.Z + r0 * Math.Sin(a)), 0f, 1f, 0f, MarkingMesher.White, u, 1f);
                decals.AddVertex((float)(isl.X + r1 * Math.Cos(a)), y, (float)(isl.Z + r1 * Math.Sin(a)), 0f, 1f, 0f, MarkingMesher.White, u, 1f);
            }
            for (int k = 0; k < N; k++)
            {
                int a = first + 2 * k, b = first + 2 * ((k + 1) % N);
                RoadSweep.Quad(decals, a, b, b + 1, a + 1);
            }
            int c = decals.VertexCount;
            decals.AddVertex((float)isl.X, y, (float)isl.Z, 0f, 1f, 0f, MarkingMesher.White, u, 1f);
            for (int k = 0; k < 10; k++)
            {
                double a = 2 * Math.PI * k / 10;
                decals.AddVertex((float)(isl.X + 0.6 * Math.Cos(a)), y, (float)(isl.Z + 0.6 * Math.Sin(a)), 0f, 1f, 0f, MarkingMesher.White, u, 1f);
            }
            for (int k = 0; k < 10; k++) RoadSweep.Tri(decals, c, c + 1 + k, c + 1 + (k + 1) % 10, 0, 1, 0);
        }

        /// <summary>Zebras and stop lines on every arm at least 6 m wide of a signalised or police chowk, on the arm's drawn
        /// ribbon just outside the cap.</summary>
        private static void ChowkMarkings(TileData t, RoadLayout layout, RoadGrade grade, JunctionCap cap, RoadOptions o, MeshData decals)
        {
            for (int k = 0; k < cap.ArmRoads.Length; k++)
            {
                int ri = cap.ArmRoads[k];
                RoadRecord r = t.Roads[ri];
                double sEdge = AlongOfCut(layout, ri, cap, k, cap.ArmForward[k]);
                RoadProfile p = layout.ProfileAt(ri, sEdge);
                if (p.DrawnM < 6f) continue;
                double dir = cap.ArmForward[k] ? 1 : -1;
                double depth = Math.Max(2.0, Math.Min(4.0, Math.Max(p.FootpathLeftM, p.FootpathRightM)));
                double z0 = sEdge + dir * 0.5, z1 = sEdge + dir * (0.5 + depth);
                if (z1 < 0 || z1 > layout.Profiles[ri].LengthM) continue;
                RibbonFrames f = RibbonMesher.FramesFor(t, ri, layout, grade, o);
                if (f == null) continue;
                MarkingMesher.Zebra(f, layout, ri, Math.Min(z0, z1), Math.Max(z0, z1), decals);
                // Stop line for traffic arriving toward the junction: on an outward arm that is the half on the arm's
                // right (left-hand traffic keeps left when driving in, which is the arm's right seen outward).
                double sStop = sEdge + dir * (0.5 + depth + 2.5);
                if (sStop < 0 || sStop > layout.Profiles[ri].LengthM) continue;
                RoadProfile ps = layout.ProfileAt(ri, sStop);
                bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
                double half = 0.5 * ps.DrawnM, sh = ps.DrawnShiftM;
                double a, b;
                if (oneway)
                {
                    a = sh - half;
                    b = sh + half;
                }
                else if (cap.ArmForward[k])
                {
                    a = sh - half; // inbound traffic on a forward arm drives against the point order: piece's right half
                    b = sh;
                }
                else
                {
                    a = sh;
                    b = sh + half;
                }
                MarkingMesher.CrossLine(f, sStop, 0.2, a, b, MarkingMesher.White, decals);
            }
        }
    }
}
