using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Meshing
{
    /// <summary>Where a junction cap cuts one road piece: the carriageway edge points of the ribbon's end section
    /// (tile-local metres), shared exactly by the ribbon and the cap.</summary>
    public struct RoadCut
    {
        public double S;
        public double CX, CZ;

        /// <summary>Unit left normal of the piece at the cut (the section direction).</summary>
        public double UX, UZ;

        public double Half, Shift;
    }

    /// <summary>A junction cap (W2_DESIGN 4.7): a polygon around the node from the game widths of every arm, with
    /// circular kerb returns between neighbouring arms. Ribbons stop at the cap edge.</summary>
    public sealed class JunctionCap
    {
        public double X, Z;

        /// <summary>Counter-clockwise outline (seen from above), tile-local metres; fan-triangulated from (X, Z).</summary>
        public double[] PolyX, PolyZ;

        public int Count;
        public int[] ArmRoads;
        public bool[] ArmForward;
        public double[] ArmSetback;

        /// <summary>Per arm: the raw along value of the arm road's cut section (where its ribbon stops).</summary>
        public double[] ArmCutS;

        /// <summary>Per arm k: the polygon indices of its cut section's right and left points (seen from the node
        /// looking out along the arm). The outline from arm k's left point to arm k + 1's right point is the kerb line
        /// of corner k (straight runs along the edges and the kerb-return arc).</summary>
        public int[] ArmRightIndex, ArmLeftIndex;

        /// <summary>Per corner k (between arm k and arm k + 1): the kerb-return radius drawn (0 = no arc).</summary>
        public float[] CornerRadiusM;

        /// <summary>The piece drawn on top (highest class): the cap takes its surface colour and lift.</summary>
        public int TopRoad;

        public JunctionKind Kind;
        public byte Flags;
        public float WidestArmM;
    }

    /// <summary>What a road island is.</summary>
    public enum IslandKind : byte
    {
        Roundabout = 0,
        Mini = 1,
        Synthetic = 2,
    }

    /// <summary>A raised road island (roundabout centre, synthetic chowk island) or a painted mini roundabout.</summary>
    public struct RoadIsland
    {
        public double X, Z;
        public float RadiusM;

        /// <summary>Mountable apron inside the island edge (buses), 0 for none.</summary>
        public float ApronM;

        public IslandKind Kind;
        public bool PolicePodium;
        public int TopRoad;

        /// <summary>Width of the planting strip inside the inner kerb (soil and low flowers along the edge).</summary>
        public float PlantingM;

        /// <summary>Index into <see cref="RoadLayout.Rings"/> of the ring around this island, or -1.</summary>
        public int Ring;

        /// <summary>Radius of the free interior the ornaments package fills (statues, gardens, fountains): inside the
        /// apron, the inner kerb and the planting strip.</summary>
        public float InteriorRadiusM
        {
            get { return Math.Max(0f, RadiusM - ApronM - RoadLayout.IslandKerbTopM - PlantingM); }
        }
    }

    /// <summary>
    /// The per-tile road layout shared by <see cref="RoadMesher"/>, <see cref="JunctionMesher"/>, <see cref="MarkingMesher"/>
    /// and <see cref="Roads.RoadCorridorIndex"/>: each piece's attributes (RATR, or on W1 packs the tile's own area type and
    /// building corridor), its structure record, its width profile from <see cref="RoadWidthModel"/>, knee joins between
    /// pieces that continue each other, true-circle roundabouts (<see cref="RoadRing"/>), junction caps with circular kerb
    /// returns and the ribbon cuts they imply, road islands (mapped rings from JNCT or closed ring ways, synthetic
    /// islands) and the smoothed centreline of every piece (<see cref="RoadCentreline"/>). Built once per decoded tile
    /// and cached (<see cref="For"/>); immutable afterwards, so worker threads may share it. Deterministic.
    /// </summary>
    public sealed partial class RoadLayout
    {
        /// <summary>Junction nodes closer than this to the tile border get no cap (a node on the border belongs to both
        /// tiles); a cap whose outline leaves the tile is dropped as well.</summary>
        public const double CapBorderMarginM = 1.0;

        /// <summary>Largest cap setback along an arm.</summary>
        public const double MaxSetbackM = 30.0;

        /// <summary>Longest straight step of a centreline.</summary>
        public const double CentreMaxSegM = 8.0;

        /// <summary>Top width of island and median kerbs.</summary>
        public const float IslandKerbTopM = 0.25f;

        /// <summary>Height of a raised island's grass interior above the surrounding road surface (the ornaments package
        /// stands statues, gardens and podiums on it, inside <see cref="RoadIsland.InteriorRadiusM"/>).</summary>
        public const float IslandInteriorHeightM = 0.30f;

        /// <summary>At a knee the narrower piece widens by at most this (1 : 20 ramp, as before); where the two widths differ
        /// by more, the wider piece tapers down at 1 : <see cref="KneeTaperRatio"/> to meet it, so the drawn width is
        /// continuous across every two-piece node (no rectangular notch).</summary>
        public const float MaxKneeWidthStepM = 3f;

        /// <summary>Width change per length of the wider piece's taper at a knee: 1 m of width over this many metres (each
        /// edge flares at 1 : 12, a kerb-line flare rather than a step).</summary>
        public const float KneeTaperRatio = 6f;

        private static readonly ConditionalWeakTable<TileData, RoadLayout> Cache = new ConditionalWeakTable<TileData, RoadLayout>();

        public readonly TileData Tile;
        public readonly bool FromRatr;

        /// <summary>The RATR corridors are the detail-pass final game corridors (<see cref="RoadWidthModel.UsesFinalCorridor"/>):
        /// carriageway and footpaths share them without a further clearance and they are not tightened again.</summary>
        public readonly bool FinalCorridor;

        /// <summary>Per road: the surface it is drawn with (colour and material channel; heritage stone or brick paving in
        /// old-core lanes).</summary>
        public readonly RoadPaving[] Paving;
        public readonly RoadAttrRecord[] Attrs;
        public readonly RoadStructureRecord[] Structures;
        public readonly RoadWidthProfile[] Profiles;
        public readonly List<JunctionCap> Caps = new List<JunctionCap>();
        public readonly List<RoadIsland> Islands = new List<RoadIsland>();

        /// <summary>True-circle roundabouts.</summary>
        public readonly List<RoadRing> Rings = new List<RoadRing>();

        /// <summary>Per road: the cuts at junction caps and ring entries in ascending S, in pairs (gap start, gap end);
        /// null = none.</summary>
        public readonly RoadCut[][] Cuts;

        /// <summary>Per road, parallel to <see cref="Cuts"/>: the piece whose lift the cap or ring owning each cut takes
        /// (the cap's top road, the ring's top road); null where <see cref="Cuts"/> is null.</summary>
        public readonly int[][] CutTops;

        /// <summary>Per road: the smoothed centreline the meshers draw.</summary>
        public readonly RoadCentreline[] Centres;

        /// <summary>Per road: index into <see cref="Rings"/> when the piece is a ring way that ring draws (no ribbon),
        /// else -1.</summary>
        public readonly int[] RingOf;

        /// <summary>Per road: knee joins at the start (index 2i) and end (2i + 1).</summary>
        public readonly RoadKnee[] Knees;

        /// <summary>Per road: raw along pairs (start, end) of the stretches absorbed into a neighbouring street (a mapped
        /// sidewalk drawn as that street's footpath, a footway crossing its carriageway); null = none
        /// (<see cref="InAbsorbed"/>).</summary>
        public readonly double[][] Absorbed;

        // Border-column shoulder width at each piece's start and end cut (NaN where the end is not a tile-border cut).
        private readonly float[] _endShoulder;

        /// <summary>Per road: cumulative along distance of each point from the first rendered point (context points
        /// get negative or beyond-length values).</summary>
        private readonly double[][] _along;

        /// <summary>The layout of a tile (built on first use, then cached for the tile's lifetime).</summary>
        public static RoadLayout For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new RoadLayout(k));
        }

        private RoadLayout(TileData t) : this(t, null)
        {
        }

        private RoadLayout(TileData t, float[][] corridorShifts)
        {
            Tile = t;
            _shiftOverride = corridorShifts;
            int n = t.Roads.Count;
            Absorbed = new double[n][];
            Attrs = new RoadAttrRecord[n];
            Structures = new RoadStructureRecord[n];
            Profiles = new RoadWidthProfile[n];
            Cuts = new RoadCut[n][];
            CutTops = new int[n][];
            Centres = new RoadCentreline[n];
            RingOf = new int[n];
            Knees = new RoadKnee[2 * n];
            _endShoulder = new float[2 * n];
            for (int i = 0; i < _endShoulder.Length; i++) _endShoulder[i] = float.NaN;
            _along = new double[n][];
            FromRatr = t.HasRoadAttrs;
            FinalCorridor = RoadWidthModel.UsesFinalCorridor(t);
            Paving = new RoadPaving[n];
            for (int i = 0; i < n; i++)
            {
                _along[i] = AlongOf(t.Roads[i]);
                Structures[i] = t.RoadStructureOf(i);
                RingOf[i] = -1;
            }
            if (FromRatr)
            {
                for (int i = 0; i < n; i++) Attrs[i] = t.RoadAttrs[i];
                if (!FinalCorridor) TightenCorridors(t);
            }
            else
            {
                LocalAttrs(t);
            }
            for (int i = 0; i < n; i++)
            {
                Profiles[i] = new RoadWidthProfile();
                RoadWidthModel.BuildProfile(t.Roads[i], Attrs[i], Profiles[i], FinalCorridor);
                Profiles[i].NoCars = !Structures[i].Has(RoadStructureFlags.CarAccessible);
                Paving[i] = RoadMaterials.PavingOf(t, i, Attrs[i]);
                ApplyCorridorShift(i);
            }
            Passages(t);
            var cutEnds = new float[4 * n];
            for (int i = 0; i < n; i++)
            {
                cutEnds[4 * i] = Profiles[i].Width[0];
                cutEnds[4 * i + 1] = Profiles[i].Width[Math.Max(0, Profiles[i].Count - 1)];
                cutEnds[4 * i + 2] = Profiles[i].Drawn[0];
                cutEnds[4 * i + 3] = Profiles[i].Drawn[Math.Max(0, Profiles[i].Count - 1)];
            }
            FindKnees(t);
            ClampParallel(t);
            AbsorbSidewalks(t);
            RingsAndIslands(t);
            KeepCutEnds(t, cutEnds);
            MatchKneeEnds(t);
            BorderCrossSections(t);
            Junctions(t);
            KneeTangents(t);
            for (int i = 0; i < n; i++) Centres[i] = BuildCentre(t, i);
            _nodeSets.Clear();
        }

        /// <summary>
        /// Tile-border cut ends keep the width <see cref="RoadWidthModel.BuildProfile(RoadRecord, in RoadAttrRecord, RoadWidthProfile, bool)"/>
        /// pinned them to (the neighbouring tile cannot see this tile's knees and rings): a knee or ring widening that
        /// reached a cut end is ramped back down to it at 1 : 20.
        /// </summary>
        private void KeepCutEnds(TileData t, float[] ends)
        {
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                RoadWidthProfile p = Profiles[i];
                float k = p.StepM / RoadWidthModel.TaperRatio;
                for (int e = 0; e < 2; e++)
                {
                    if (e == 0 ? !r.HasPrevContext : !r.HasNextContext) continue;
                    float w = ends[4 * i + e], wd = ends[4 * i + 2 + e];
                    for (int j = 0; j < p.Count; j++)
                    {
                        int d = e == 0 ? j : p.Count - 1 - j;
                        p.Width[j] = Math.Min(p.Width[j], w + k * d);
                        p.Drawn[j] = Math.Min(p.Drawn[j], wd + k * d);
                    }
                }
            }
        }

        /// <summary>Distance from a tile-border cut within which a piece's shoulders follow the border cross-section.</summary>
        public const double BorderBlendM = 20.0;

        /// <summary>
        /// Tile-border cut ends get the tile-independent cross-section both tiles agree on
        /// (<see cref="RoadWidthModel.BorderArea"/> at the cut): the footpaths are pinned to
        /// <see cref="RoadWidthModel.BorderFootpaths"/> there and ramp to this tile's own at 1 : 20; the shoulders within
        /// <see cref="BorderBlendM"/> of the cut take the border column (<see cref="ShoulderAt"/>).
        /// </summary>
        private void BorderCrossSections(TileData t)
        {
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                _endShoulder[2 * i] = _endShoulder[2 * i + 1] = float.NaN;
                if (!r.HasPrevContext && !r.HasNextContext) continue;
                RoadWidthProfile p = Profiles[i];
                float k = p.StepM / RoadWidthModel.TaperRatio;
                for (int e = 0; e < 2; e++)
                {
                    if (e == 0 ? !r.HasPrevContext : !r.HasNextContext) continue;
                    int pi = e == 0 ? 1 : r.PointCount - 2;
                    double lon, lat;
                    WorldFrame.GameToLonLat(t.Tile.X0 + r.Points[2 * pi] / 100.0, t.Tile.Z0 + r.Points[2 * pi + 1] / 100.0, out lon, out lat);
                    AreaType area = RoadWidthModel.BorderArea(lon, lat);
                    _endShoulder[2 * i + e] = RoadWidthModel.ShoulderM(r.RoadClass, area);
                    float fl, fr;
                    RoadWidthModel.BorderFootpaths(r, Attrs[i], area, out fl, out fr);
                    if (Attrs[i].Has(RoadAttrFlags.Dual)) fr = 0f;
                    for (int j = 0; j < p.Count; j++)
                    {
                        int d = e == 0 ? j : p.Count - 1 - j;
                        p.FootLeft[j] = Math.Max(fl - k * d, Math.Min(fl + k * d, p.FootLeft[j]));
                        p.FootRight[j] = Math.Max(fr - k * d, Math.Min(fr + k * d, p.FootRight[j]));
                    }
                }
                for (int j = 0; j < p.Count; j++)
                {
                    if (p.FootLeft[j] < RoadWidthModel.MinFootpathM) p.FootLeft[j] = 0f;
                    if (p.FootRight[j] < RoadWidthModel.MinFootpathM) p.FootRight[j] = 0f;
                }
            }
        }

        /// <summary>The shoulder width of road <paramref name="road"/> at raw along <paramref name="alongM"/>: the border
        /// column within <see cref="BorderBlendM"/> of a tile-border cut, else the piece's own area column.</summary>
        public float ShoulderAt(int road, double alongM)
        {
            float start = _endShoulder[2 * road], end = _endShoulder[2 * road + 1];
            if (!float.IsNaN(start) && alongM <= BorderBlendM) return start;
            if (!float.IsNaN(end) && alongM >= Profiles[road].LengthM - BorderBlendM) return end;
            return RoadWidthModel.ShoulderM(Tile.Roads[road].RoadClass, RoadWidthModel.AreaOf(Attrs[road]));
        }

        /// <summary>The widest shoulder of road <paramref name="road"/> anywhere along it.</summary>
        private float MaxShoulder(int road)
        {
            float m = RoadWidthModel.ShoulderM(Tile.Roads[road].RoadClass, RoadWidthModel.AreaOf(Attrs[road]));
            if (!float.IsNaN(_endShoulder[2 * road])) m = Math.Max(m, _endShoulder[2 * road]);
            if (!float.IsNaN(_endShoulder[2 * road + 1])) m = Math.Max(m, _endShoulder[2 * road + 1]);
            return m;
        }

        /// <summary>The carriageway half width of road <paramref name="road"/> at <paramref name="alongM"/> in the W2_DESIGN 4.2
        /// model (<see cref="RoadWidthProfile.WidthAt"/>): what lanes, traffic and the driving index of stage one use. The
        /// surface as drawn (wider on gallis, narrower or moved beside an unconnected parallel piece, with junction caps and
        /// rings) is <see cref="DrawnHalfWidthAt"/> about <see cref="RoadWidthProfile.ShiftAt"/>, and
        /// <see cref="RoadSurfaceQuery"/> answers it for any point.</summary>
        public float HalfWidthAt(int road, double alongM)
        {
            return 0.5f * Profiles[road].WidthAt(alongM);
        }

        /// <summary>The drawn carriageway half width of road <paramref name="road"/> at <paramref name="alongM"/>: what the
        /// ribbon covers about the shifted centre (the 4.2 width raised to the rideability floor, joined at knees, widened
        /// at true rings and clamped against unconnected parallel pieces).</summary>
        public float DrawnHalfWidthAt(int road, double alongM)
        {
            return 0.5f * Profiles[road].DrawnAt(alongM);
        }

        /// <summary>The full cross-section of road <paramref name="road"/> at <paramref name="alongM"/>.</summary>
        public RoadProfile ProfileAt(int road, double alongM)
        {
            return RoadWidthModel.ProfileFrom(Tile.Roads[road], Attrs[road], Profiles[road], alongM);
        }

        /// <summary>Along distance of point <paramref name="pointIndex"/> of road <paramref name="road"/>.</summary>
        public double AlongAt(int road, int pointIndex)
        {
            return _along[road][pointIndex];
        }

        /// <summary>True when <paramref name="alongM"/> lies inside a junction cap or ring entry of the road.</summary>
        public bool InGap(int road, double alongM)
        {
            RoadCut[] c = Cuts[road];
            if (c == null) return false;
            for (int k = 0; k + 1 < c.Length; k += 2)
                if (alongM > c[k].S && alongM < c[k + 1].S) return true;
            return false;
        }

        /// <summary>True when the piece gets a ribbon (not a ring way a true-circle ring replaces).</summary>
        public bool DrawsRibbon(int road)
        {
            return RingOf[road] < 0;
        }

        /// <summary>The piece is drawn at deck heights (bridge or flyover with deck heights).</summary>
        public bool IsElevated(int road)
        {
            return Structures[road].IsElevated;
        }

        private static double[] AlongOf(RoadRecord r)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0;
            var s = new double[count];
            for (int i = first + 1; i < count; i++)
            {
                double dx = (p[2 * i] - p[2 * i - 2]) / 100.0, dz = (p[2 * i + 1] - p[2 * i - 1]) / 100.0;
                s[i] = s[i - 1] + Math.Sqrt(dx * dx + dz * dz);
            }
            if (first == 1)
            {
                double dx = (p[2] - p[0]) / 100.0, dz = (p[3] - p[1]) / 100.0;
                s[0] = -Math.Sqrt(dx * dx + dz * dz);
            }
            return s;
        }

        private static void Rendered(RoadRecord r, out int first, out int last)
        {
            int count = r.Points.Length / 2;
            first = r.HasPrevContext ? 1 : 0;
            last = r.HasNextContext ? count - 2 : count - 1;
        }

        private static long Key(int xcm, int zcm)
        {
            return (long)xcm << 32 ^ (uint)zcm;
        }

        // -------------------------------------------------------------------------------------------------------
        // W1 packs: area type and corridor from the tile itself
        // -------------------------------------------------------------------------------------------------------

        private const int AreaCells = 8;

        /// <summary>
        /// RATR corridors are building-to-building totals, but the ribbon is centred on the way: a building 3 m from
        /// the centreline on one side with open ground on the other still reads as a wide corridor. Take, per
        /// sample, the smaller of the RATR value and twice the nearer side's distance to this tile's buildings, so
        /// a centred ribbon wider than the floor does not cover a building it need not (V2). Within the border margin
        /// the local value never goes under the access width the cut is pinned to, so both tiles still meet at the same
        /// width.
        /// </summary>
        private void TightenCorridors(TileData t)
        {
            if (t.Buildings.Count == 0) return;
            var corridor = new RoadCorridor(t);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                RoadAttrRecord a = Attrs[i];
                double radius = 0.5 * RoadWidthModel.NominalGameWidthM(r, a) + 5.5; // + footpath reach
                float pinned = RoadWidthModel.BorderAccessWidthM(r);
                int[] local = corridor.Sample(r, radius, -(int)Math.Ceiling((pinned + RoadWidthModel.CorridorClearanceM) * 10)); // seams: see Sample
                int[] src = a.CorridorDm ?? new int[0];
                int len = Math.Max(src.Length, local.Length);
                if (len == 0) continue;
                var merged = new int[len];
                bool changed = false;
                for (int k = 0; k < len; k++)
                {
                    int x = k < src.Length ? src[k] : 0, y = k < local.Length ? local[k] : 0;
                    int m = x == 0 ? y : y == 0 ? x : Math.Min(x, y);
                    merged[k] = m;
                    if (k >= src.Length || m != x) changed = true;
                }
                if (!changed) continue;
                a.CorridorDm = merged;
                Attrs[i] = a;
            }
        }

        private void LocalAttrs(TileData t)
        {
            AreaType[] cells = AreaTypeGrid.ClassifyTile(t, AreaCells);
            RoadCorridor corridor = t.Buildings.Count > 0 ? new RoadCorridor(t) : null;
            double cell = t.Tile.Size / AreaCells;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                int[] p = r.Points;
                int mid = p.Length / 4;
                int ci = (int)(p[2 * mid] / 100.0 / cell), cj = (int)(p[2 * mid + 1] / 100.0 / cell);
                ci = ci < 0 ? 0 : ci >= AreaCells ? AreaCells - 1 : ci;
                cj = cj < 0 ? 0 : cj >= AreaCells ? AreaCells - 1 : cj;
                var a = new RoadAttrRecord { Area = cells[cj * AreaCells + ci], CorridorDm = new int[0] };
                if (a.Area == AreaType.Unknown) a.Area = AreaType.Rural;
                if (corridor != null)
                {
                    float nominal = RoadWidthModel.NominalGameWidthM(r, a);
                    double radius = 0.5 * nominal + 1.5 + 2.0; // + footpath reach
                    int borderDm = -(int)Math.Ceiling((RoadWidthModel.BorderAccessWidthM(r) + RoadWidthModel.CorridorClearanceM) * 10); // seams
                    a.CorridorDm = corridor.Sample(r, radius, borderDm);
                }
                Attrs[i] = a;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Knee joins
        // -------------------------------------------------------------------------------------------------------

        private static bool KneeClass(RoadClass c)
        {
            return c != RoadClass.Steps;
        }

        /// <summary>
        /// Find the points where exactly two drawn pieces end and nothing else passes (an OSM way continued by another),
        /// and give the join one width: the narrower end widens (1 : 20 ramp) by up to <see cref="MaxKneeWidthStepM"/>,
        /// the wider end tapers down to meet it (1 : <see cref="KneeTaperRatio"/>); footpaths on the same side meet at the
        /// narrower of the two. Tile-border cut ends keep their width (both tiles agree on it).
        /// </summary>
        private void FindKnees(TileData t)
        {
            var ends = new Dictionary<long, List<int>>();
            var passing = new HashSet<long>();
            var order = new List<long>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!RoadMesher.IsDrawn(r, null)) continue;
                int first, last;
                Rendered(r, out first, out last);
                int[] p = r.Points;
                for (int k = first; k <= last; k++)
                {
                    long key = Key(p[2 * k], p[2 * k + 1]);
                    bool end = k == first && !r.HasPrevContext || k == last && !r.HasNextContext;
                    if (!end)
                    {
                        passing.Add(key);
                        continue;
                    }
                    List<int> l;
                    if (!ends.TryGetValue(key, out l))
                    {
                        ends[key] = l = new List<int>(2);
                        order.Add(key);
                    }
                    l.Add(i << 1 | (k == first ? 0 : 1));
                }
            }
            foreach (long key in order)
            {
                List<int> l = ends[key];
                if (l.Count != 2 || passing.Contains(key)) continue;
                int ca = l[0], cb = l[1];
                int ia = ca >> 1, ib = cb >> 1;
                if (ia == ib) continue;
                RoadRecord ra = t.Roads[ia], rb = t.Roads[ib];
                if (!KneeClass(ra.RoadClass) || !KneeClass(rb.RoadClass)) continue;
                if (RoadWidthModel.IsFootClass(ra.RoadClass) != RoadWidthModel.IsFootClass(rb.RoadClass)) continue;
                if (Attrs[ia].Has(RoadAttrFlags.RingMember) || Attrs[ib].Has(RoadAttrFlags.RingMember)) continue;
                if (IsPassage(ia) || IsPassage(ib)) continue;
                bool aStart = (ca & 1) == 0, bStart = (cb & 1) == 0;
                RoadWidthProfile pa = Profiles[ia], pb = Profiles[ib];
                float wa = aStart ? pa.Drawn[0] : pa.Drawn[pa.Count - 1], wb = bStart ? pb.Drawn[0] : pb.Drawn[pb.Count - 1];
                bool deckA = Structures[ia].IsElevated || (ra.Flags & RoadFlags.Bridge) != 0;
                bool deckB = Structures[ib].IsElevated || (rb.Flags & RoadFlags.Bridge) != 0;
                // Different layers on the ground (an OSM layer tag on a road over a culvert, a lane under an arcade) still
                // continue each other; decks at different layers do not.
                if (deckA != deckB || deckA && ra.Layer != rb.Layer || Structures[ia].IsElevated || Structures[ib].IsElevated)
                {
                    // A street meeting a deck end (bridge, flyover): no shared fillet (the deck keeps its mapped line and
                    // width, the bridges package builds on it), but a wider street tapers down to the deck's width so the
                    // join has no rectangular notch.
                    if (deckA != deckB)
                    {
                        if (!deckA && wa > wb) Taper(ra, pa, aStart, wb);
                        if (!deckB && wb > wa) Taper(rb, pb, bStart, wa);
                    }
                    continue;
                }
                float w = Math.Min(Math.Max(wa, wb), Math.Min(wa, wb) + MaxKneeWidthStepM);
                Pin(pa, aStart, w);
                Pin(pb, bStart, w);
                Taper(ra, pa, aStart, w);
                Taper(rb, pb, bStart, w);
                MatchKneeFootpaths(pa, aStart, pb, bStart);
                int liftRoad = RoadMesher.LiftOf(ra, null) >= RoadMesher.LiftOf(rb, null) ? ia : ib;
                double ax, az, bx, bz;
                EndDirection(ra, aStart, out ax, out az);
                EndDirection(rb, bStart, out bx, out bz);
                Knees[2 * ia + (aStart ? 0 : 1)] = new RoadKnee
                {
                    Has = true, Other = ib, OtherStarts = bStart, OutX = bx, OutZ = bz, WidthM = w, LiftRoad = liftRoad,
                };
                Knees[2 * ib + (bStart ? 0 : 1)] = new RoadKnee
                {
                    Has = true, Other = ia, OtherStarts = aStart, OutX = ax, OutZ = az, WidthM = w, LiftRoad = liftRoad,
                };
            }
        }

        /// <summary>Unit direction from a piece end into the piece (away from the end point).</summary>
        private static void EndDirection(RoadRecord r, bool atStart, out double dx, out double dz)
        {
            int first, last;
            Rendered(r, out first, out last);
            int[] p = r.Points;
            int k = atStart ? first : last, step = atStart ? 1 : -1;
            int q = k + step;
            while (q > first && q < last && Sq((p[2 * q] - p[2 * k]) / 100.0) + Sq((p[2 * q + 1] - p[2 * k + 1]) / 100.0) < 1e-4) q += step;
            dx = (p[2 * q] - p[2 * k]) / 100.0;
            dz = (p[2 * q + 1] - p[2 * k + 1]) / 100.0;
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                dx = 1;
                dz = 0;
                return;
            }
            dx /= l;
            dz /= l;
        }

        /// <summary>Raise a profile's drawn end to width w and ramp down from it at 1 : 20 (the drawn width only: lanes keep
        /// their own widths).</summary>
        private static void Pin(RoadWidthProfile p, bool atStart, float w)
        {
            float k = p.StepM / RoadWidthModel.TaperRatio;
            for (int i = 0; i < p.Count; i++)
            {
                int d = atStart ? i : p.Count - 1 - i;
                p.Drawn[i] = Math.Max(p.Drawn[i], w - k * d);
            }
        }

        /// <summary>After the parallel-piece clamp narrowed one end of a knee, the other end tapers down to it too, so every
        /// knee still has one width.</summary>
        private void MatchKneeEnds(TileData t)
        {
            for (int i = 0; i < t.Roads.Count; i++)
            {
                for (int e = 0; e < 2; e++)
                {
                    RoadKnee kn = Knees[2 * i + e];
                    if (!kn.Has || kn.Other < i) continue;
                    RoadWidthProfile pa = Profiles[i], pb = Profiles[kn.Other];
                    bool aStart = e == 0, bStart = kn.OtherStarts;
                    float wa = aStart ? pa.Drawn[0] : pa.Drawn[pa.Count - 1], wb = bStart ? pb.Drawn[0] : pb.Drawn[pb.Count - 1];
                    if (Math.Abs(wa - wb) < 1e-3f) continue;
                    if (wa > wb) Taper(t.Roads[i], pa, aStart, wb);
                    else Taper(t.Roads[kn.Other], pb, bStart, wa);
                    // A short piece whose other end is a tile-border cut may not narrow that far (the cut keeps its width):
                    // the narrower end widens to meet it instead.
                    wa = aStart ? pa.Drawn[0] : pa.Drawn[pa.Count - 1];
                    wb = bStart ? pb.Drawn[0] : pb.Drawn[pb.Count - 1];
                    if (wa > wb + 1e-3f) Pin(pb, bStart, wa);
                    else if (wb > wa + 1e-3f) Pin(pa, aStart, wb);
                }
            }
        }

        /// <summary>Lower a profile's drawn end to width w, ramping up from it at 1 : <see cref="KneeTaperRatio"/>; a
        /// tile-border cut end at the other end keeps its width (1 : 20 back down from it).</summary>
        private static void Taper(RoadRecord r, RoadWidthProfile p, bool atStart, float w)
        {
            float kt = p.StepM / KneeTaperRatio, k = p.StepM / RoadWidthModel.TaperRatio;
            float end0 = p.Drawn[0], end1 = p.Drawn[p.Count - 1];
            for (int i = 0; i < p.Count; i++)
            {
                int d = atStart ? i : p.Count - 1 - i;
                p.Drawn[i] = Math.Min(p.Drawn[i], w + kt * d);
            }
            for (int i = 0; i < p.Count; i++)
            {
                if (r.HasPrevContext && !atStart) p.Drawn[i] = Math.Max(p.Drawn[i], end0 - k * i);
                if (r.HasNextContext && atStart) p.Drawn[i] = Math.Max(p.Drawn[i], end1 - k * (p.Count - 1 - i));
            }
        }

        /// <summary>The footpaths of two knee ends on the same side (left meets left when one piece starts and the other
        /// ends there, else left meets right) meet at the narrower of the two, tapered at 1 : 20.</summary>
        private static void MatchKneeFootpaths(RoadWidthProfile pa, bool aStart, RoadWidthProfile pb, bool bStart)
        {
            bool same = aStart != bStart;
            MatchFoot(pa.FootLeft, pa, aStart, same ? pb.FootLeft : pb.FootRight, pb, bStart);
            MatchFoot(pa.FootRight, pa, aStart, same ? pb.FootRight : pb.FootLeft, pb, bStart);
        }

        private static void MatchFoot(float[] fa, RoadWidthProfile pa, bool aStart, float[] fb, RoadWidthProfile pb, bool bStart)
        {
            float ea = aStart ? fa[0] : fa[pa.Count - 1], eb = bStart ? fb[0] : fb[pb.Count - 1];
            if (ea <= 0f || eb <= 0f || Math.Abs(ea - eb) < 1e-3f) return;
            float target = Math.Max(RoadWidthModel.MinFootpathM, Math.Min(ea, eb));
            LowerFrom(fa, pa, aStart, target);
            LowerFrom(fb, pb, bStart, target);
        }

        private static void LowerFrom(float[] v, RoadWidthProfile p, bool atStart, float target)
        {
            float k = p.StepM / RoadWidthModel.TaperRatio;
            for (int i = 0; i < p.Count; i++)
            {
                if (v[i] <= 0f) continue;
                int d = atStart ? i : p.Count - 1 - i;
                v[i] = Math.Max(RoadWidthModel.MinFootpathM, Math.Min(v[i], target + k * d));
            }
        }

        /// <summary>Shared fillet tangent of every knee, from both pieces' end segments (identical for both ends).</summary>
        private void KneeTangents(TileData t)
        {
            // Wanted tangents first (a single-segment piece with a knee at both ends shares its segment between them).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    for (int e = 0; e < 2; e++)
                    {
                        RoadKnee k = Knees[2 * i + e];
                        if (!k.Has || k.Other < i) continue; // each pair once, from its lower piece
                        KneeTangent(t, i, e, pass == 1);
                    }
                }
            }
        }

        /// <summary>The wanted (and, when <paramref name="allocate"/>, the allocated) tangent of the knee at end
        /// <paramref name="e"/> of piece <paramref name="i"/>, written to both pieces' knee records.</summary>
        private void KneeTangent(TileData t, int i, int e, bool allocate)
        {
            RoadKnee k = Knees[2 * i + e];
            bool aStart = e == 0;
            int j = k.Other;
            double ix, iz, ox, oz;
            EndDirection(t.Roads[i], aStart, out ix, out iz); // into piece i, away from the knee
            EndDirection(t.Roads[j], k.OtherStarts, out ox, out oz);
            // The curve runs from piece i into piece j: in-direction is -i's end direction, out is j's.
            double inx = -ix, inz = -iz;
            double th = Math.Atan2(Math.Abs(inx * oz - inz * ox), inx * ox + inz * oz);
            double ri = Radius(t, i), rj = Radius(t, j);
            double di = RoadCentreline.MaxDeviationM(t.Roads[i].RoadClass, RoadWidthModel.AreaOf(Attrs[i]));
            double dj = RoadCentreline.MaxDeviationM(t.Roads[j].RoadClass, RoadWidthModel.AreaOf(Attrs[j]));
            bool dev;
            double want = RoadCentreline.WantedTangent(th, Math.Min(ri, rj), Math.Min(di, dj), out dev);
            double tan = 0;
            if (want > 0 && allocate)
            {
                tan = Math.Min(want, EndRoom(t, i, aStart, want));
                tan = Math.Min(tan, EndRoom(t, j, k.OtherStarts, want));
                if (tan < 1e-3) tan = 0;
            }
            k.TangentM = tan;
            k.WantM = want;
            Knees[2 * i + e] = k;
            RoadKnee o = Knees[2 * j + (k.OtherStarts ? 0 : 1)];
            o.TangentM = tan;
            o.WantM = want;
            Knees[2 * j + (k.OtherStarts ? 0 : 1)] = o;
        }

        private double Radius(TileData t, int i)
        {
            return RoadCentreline.DesignRadiusM(t.Roads[i].RoadClass, RoadWidthModel.AreaOf(Attrs[i]), Profiles[i].MaxDrawn);
        }

        /// <summary>
        /// The share of a piece's end segment a knee corner wanting <paramref name="want"/> may use: the whole segment,
        /// or its proportional share with the neighbouring corner, less a cut margin when the segment ends at a tile cut,
        /// and never into a junction gap.
        /// </summary>
        private double EndRoom(TileData t, int ri, bool atStart, double want)
        {
            RoadRecord r = t.Roads[ri];
            int first, last;
            Rendered(r, out first, out last);
            int[] p = r.Points;
            double[] al = _along[ri];
            // Distinct vertices from the knee end inward: v0 (the knee), v1, v2.
            int step = atStart ? 1 : -1, k0 = atStart ? first : last;
            int k1 = NextDistinct(p, k0, step, first, last);
            if (k1 < 0) return 0;
            int k2 = NextDistinct(p, k1, step, first, last);
            double len = Math.Abs(al[k1] - al[k0]);
            bool k1IsEnd = k1 == (atStart ? last : first);
            double room = len;
            if (k1IsEnd && (atStart ? r.HasNextContext : r.HasPrevContext)) room = Math.Max(0, len - RoadCentreline.CutMarginM);
            double other = 0;
            RoadKnee far = Knees[2 * ri + (atStart ? 1 : 0)];
            if (k1IsEnd && far.Has) other = far.WantM; // the knee at the other end of a single segment
            if (!k1IsEnd && k2 >= 0)
            {
                double ax = (p[2 * k1] - p[2 * k0]) / 100.0, az = (p[2 * k1 + 1] - p[2 * k0 + 1]) / 100.0;
                double bx = (p[2 * k2] - p[2 * k1]) / 100.0, bz = (p[2 * k2 + 1] - p[2 * k1 + 1]) / 100.0;
                double th = Math.Atan2(Math.Abs(ax * bz - az * bx), ax * bx + az * bz);
                bool dev;
                if (!InGapRaw(ri, al[k1], 0.5))
                    other = RoadCentreline.WantedTangent(th, Radius(t, ri), RoadCentreline.MaxDeviationM(r.RoadClass, RoadWidthModel.AreaOf(Attrs[ri])), out dev);
            }
            double share = other > 0 && want + other > len ? len * want / (want + other) : Math.Min(want, len);
            room = Math.Min(room, share);
            RoadCut[] c = Cuts[ri];
            if (c != null)
            {
                double s0 = al[k0];
                for (int g = 0; g + 1 < c.Length; g += 2)
                {
                    double d = atStart ? c[g].S - RoadCentreline.GapMarginM - s0 : s0 - c[g + 1].S - RoadCentreline.GapMarginM;
                    if (d >= 0 && d < room) room = d;
                    if (d < 0 && (atStart ? c[g + 1].S > s0 : c[g].S < s0)) room = 0;
                }
            }
            return Math.Max(0, room);
        }

        private bool InGapRaw(int ri, double s, double margin)
        {
            RoadCut[] c = Cuts[ri];
            if (c == null) return false;
            for (int g = 0; g + 1 < c.Length; g += 2)
                if (s >= c[g].S - margin && s <= c[g + 1].S + margin) return true;
            return false;
        }

        private static int NextDistinct(int[] p, int k, int step, int first, int last)
        {
            int q = k + step;
            while (q >= first && q <= last)
            {
                if (Sq((p[2 * q] - p[2 * k]) / 100.0) + Sq((p[2 * q + 1] - p[2 * k + 1]) / 100.0) >= 1e-4) return q;
                q += step;
            }
            return -1;
        }

        private RoadCentreline BuildCentre(TileData t, int i)
        {
            RoadRecord r = t.Roads[i];
            AreaType area = RoadWidthModel.AreaOf(Attrs[i]);
            RoadWidthProfile prof = Profiles[i];
            double radius = RoadCentreline.DesignRadiusM(r.RoadClass, area, prof.MaxDrawn);
            double dev = RoadCentreline.MaxDeviationM(r.RoadClass, area);
            // Ring ways and elevated decks keep their mapped shape (the ring is drawn as a circle; deck heights belong
            // to the mapped points).
            if (Attrs[i].Has(RoadAttrFlags.RingMember) || Structures[i].IsElevated) radius = 0;
            float foot = 0f;
            for (int k = 0; k < prof.Count; k++) foot = Math.Max(foot, Math.Max(prof.FootLeft[k], prof.FootRight[k]));
            double edge = 0.5 * prof.MaxDrawn + foot + MaxShoulder(i);
            return RoadCentreline.Build(r, _along[i], radius, dev, CentreMaxSegM, Cuts[i], Knees[2 * i], Knees[2 * i + 1], edge);
        }

        /// <summary>
        /// Half width of the clear corridor of road <paramref name="road"/> at raw along <paramref name="alongM"/>, on each
        /// side of the drawn (smoothed) centreline: the drawn extent (carriageway half width and its dual-carriageway shift,
        /// footpath, shoulder), and at least half of <see cref="Roads.RoadClearance.MinCorridorM"/> (decision 1: every drawn
        /// road keeps 4.8 m clear, footways included).
        /// </summary>
        public float CorridorHalfM(int road, double alongM)
        {
            float left, right;
            CorridorSides(road, alongM, out left, out right);
            return Math.Max(0.5f * Roads.RoadClearance.MinCorridorM, Math.Max(left, right));
        }

        /// <summary>
        /// How far the drawn extent of road <paramref name="road"/> reaches left and right of its (smoothed) centreline at
        /// raw along <paramref name="alongM"/>: the carriageway with its shift (<see cref="RoadWidthProfile.ShiftAt"/>),
        /// then the footpath (at least <see cref="RoadWidthModel.MinFootpathM"/> where drawn) or the shoulder.
        /// </summary>
        public void CorridorSides(int road, double alongM, out float left, out float right)
        {
            RoadWidthProfile p = Profiles[road];
            float w = p.DrawnAt(alongM);
            float shift = p.ShiftAt(alongM);
            float shoulder = ShoulderAt(road, alongM);
            // A footpath is drawn at least MinFootpathM wide wherever it is drawn at all, over the whole stretch between two
            // rows when either end has one (the ribbon's rule): the larger of the two samples around alongM, constant
            // between samples, so the corridor's linear segments hold it.
            float fl = FootReach(p, p.FootLeft, alongM), fr = FootReach(p, p.FootRight, alongM);
            left = shift + 0.5f * w + Math.Max(fl, shoulder);
            right = -shift + 0.5f * w + Math.Max(fr, shoulder);
        }

        private static float FootReach(RoadWidthProfile p, float[] foot, double alongM)
        {
            if (p.Count <= 1) return p.Count == 1 && foot[0] > 0f ? Math.Max(foot[0], RoadWidthModel.MinFootpathM) : 0f;
            int i = (int)Math.Floor(alongM / Math.Max(1e-6, p.StepM));
            i = i < 0 ? 0 : i > p.Count - 2 ? p.Count - 2 : i;
            float v = Math.Max(foot[i], foot[i + 1]);
            return v > 0f ? Math.Max(v, RoadWidthModel.MinFootpathM) : 0f;
        }

        /// <summary>
        /// Who may use road <paramref name="road"/> anywhere along it (W2_DESIGN 4.4, decision 5): the width mask of its
        /// narrowest access width (the width before the rideability floor), its OSM access, and no car, jeep or bus where
        /// its structure record says a car does not fit. The lane graph and route masks should use this rather than
        /// <see cref="RoadWidthModel.AccessFor(float, in RoadRecord, in RoadAttrRecord)"/>, which cannot see the tile's
        /// corridor semantics.
        /// </summary>
        public Travel AccessOf(int road)
        {
            RoadWidthProfile p = Profiles[road];
            Travel t = RoadWidthModel.AccessForWidth(p.MinAccessWidth, Tile.Roads[road], Attrs[road]);
            if (p.NoCars) t &= ~(Travel.Car | Travel.Jeep | Travel.Bus);
            return t;
        }

        // -------------------------------------------------------------------------------------------------------
        // Roundabouts and islands
        // -------------------------------------------------------------------------------------------------------

        private bool CapClass(int i)
        {
            RoadRecord r = Tile.Roads[i];
            return RoadWidthModel.IsMotor(r.RoadClass) && r.RoadClass != RoadClass.Track;
        }

        private static bool CapClass(RoadClass c)
        {
            return RoadWidthModel.IsMotor(c) && c != RoadClass.Track;
        }

        private void RingsAndIslands(TileData t)
        {
            var ringRoads = new List<int>();
            Dictionary<long, int> nodeCount = null;
            // Mapped rings from JNCT.
            foreach (JunctionRecord j in t.Junctions)
            {
                double jx = j.XCm / 100.0, jz = j.ZCm / 100.0;
                if (j.Kind == JunctionKind.MiniRoundabout)
                {
                    float widest = WidestNear(t, jx, jz, 12.0, null);
                    Islands.Add(new RoadIsland { X = jx, Z = jz, RadiusM = (float)Math.Max(2.0, Math.Min(3.0, 0.25 * widest)), Kind = IslandKind.Mini, TopRoad = TopNear(t, jx, jz, 12.0), Ring = -1 });
                    continue;
                }
                if ((j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular) && j.RingDiameterCm > 0)
                {
                    double ringR = j.RingDiameterCm / 200.0;
                    ringRoads.Clear();
                    for (int i = 0; i < t.Roads.Count; i++)
                        if (Attrs[i].Has(RoadAttrFlags.RingMember) && NearRing(t.Roads[i], jx, jz, ringR)) ringRoads.Add(i);
                    AddRing(t, jx, jz, 2 * ringR, j.IslandDiameterCm / 100.0, ringRoads, j.Kind == JunctionKind.Roundabout && j.Has(JunctionFlags.HasPolice));
                    continue;
                }
                if (j.Kind == JunctionKind.SyntheticIsland)
                {
                    float widest = WidestNear(t, jx, jz, 15.0, null);
                    double d = Math.Max(6.0, Math.Min(16.0, 0.7 * widest));
                    if (j.IslandDiameterCm > 0) d = j.IslandDiameterCm / 100.0;
                    // Lanes circulate round the island (LaneGraph synthesises its ring), but only the chowk node's own
                    // arms are re-routed: a second junction node or another carriageway inside the disc (split dual
                    // carriageway chowks) would run its lanes through it, so the island shrinks clear of them, and
                    // below the 6 m minimum the chowk keeps its podium without an island.
                    if (nodeCount == null) nodeCount = NodeCounts(t);
                    d = Math.Min(d, 2.0 * ClearRadius(t, j.XCm, j.ZCm, 0.5 * d, nodeCount));
                    if (d >= 6.0)
                        Islands.Add(new RoadIsland
                        {
                            X = jx, Z = jz, RadiusM = (float)(0.5 * d), Kind = IslandKind.Synthetic, PolicePodium = true, TopRoad = TopNear(t, jx, jz, 15.0),
                            PlantingM = PlantingFor(0.5 * d, 0f), Ring = -1,
                        });
                }
            }
            // W2 packs (RATR present) carry every real ring in JNCT and RING_MEMBER (W2_DESIGN 4.7: mapped rings, mini
            // nodes and curated synthetic islands only), and most of their tiles have no JNCT record at all: never guess
            // there, or parking and campus service loops become fake roundabouts with no ring lanes.
            if (t.Junctions.Count > 0 || FromRatr) return;
            // W1 packs: a closed motor way under 80 m across is a roundabout ring.
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || r.HasPrevContext || r.HasNextContext) continue;
                int[] p = r.Points;
                int n = p.Length / 2;
                if (n < 5 || p[0] != p[2 * n - 2] || p[1] != p[2 * n - 1]) continue;
                double cx = 0, cz = 0;
                for (int k = 0; k < n - 1; k++)
                {
                    cx += p[2 * k] / 100.0;
                    cz += p[2 * k + 1] / 100.0;
                }
                cx /= n - 1;
                cz /= n - 1;
                double meanR = 0, maxR = 0;
                for (int k = 0; k < n - 1; k++)
                {
                    double d = Math.Sqrt(Sq(p[2 * k] / 100.0 - cx) + Sq(p[2 * k + 1] / 100.0 - cz));
                    meanR += d;
                    maxR = Math.Max(maxR, d);
                }
                meanR /= n - 1;
                if (maxR > 40 || meanR < 4 || maxR > 1.6 * meanR) continue;
                ringRoads.Clear();
                ringRoads.Add(i);
                AddRing(t, cx, cz, 2 * meanR, 0, ringRoads, false);
            }
        }

        private static float PlantingFor(double islandRadius, float apron)
        {
            double free = islandRadius - apron - IslandKerbTopM;
            if (free < 2.0) return 0f;
            return (float)Math.Max(0.6, Math.Min(1.8, 0.12 * islandRadius));
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private static bool NearRing(RoadRecord r, double cx, double cz, double ringR)
        {
            int[] p = r.Points;
            int mid = p.Length / 4;
            double d = Math.Sqrt(Sq(p[2 * mid] / 100.0 - cx) + Sq(p[2 * mid + 1] / 100.0 - cz));
            return Math.Abs(d - ringR) < Math.Max(6.0, 0.3 * ringR);
        }

        /// <summary>How many cap-class roads pass through each point (keys as in <see cref="Junctions"/>).</summary>
        private static Dictionary<long, int> NodeCounts(TileData t)
        {
            var count = new Dictionary<long, int>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    long key = Key(p[2 * k], p[2 * k + 1]);
                    int c;
                    count.TryGetValue(key, out c);
                    count[key] = c + 1;
                }
            }
            return count;
        }

        /// <summary>
        /// The largest island radius at the junction node (xcm, zcm) that keeps 0.5 m clear of every carriageway not
        /// re-routed round it: roads that do not pass through the node, and other junction nodes (shared points) of the
        /// roads that do. Starts from <paramref name="maxR"/>.
        /// </summary>
        private double ClearRadius(TileData t, int xcm, int zcm, double maxR, Dictionary<long, int> nodeCount)
        {
            double jx = xcm / 100.0, jz = zcm / 100.0, best = maxR;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) continue;
                int[] p = r.Points;
                int n = p.Length / 2;
                bool through = false;
                for (int k = 0; k < n && !through; k++) through = p[2 * k] == xcm && p[2 * k + 1] == zcm;
                for (int k = 0; k < n; k++)
                {
                    if (p[2 * k] == xcm && p[2 * k + 1] == zcm) continue;
                    double d = Math.Sqrt(Sq(p[2 * k] / 100.0 - jx) + Sq(p[2 * k + 1] / 100.0 - jz));
                    if (d > best + 20.0) continue;
                    if (through)
                    {
                        int c;
                        nodeCount.TryGetValue(Key(p[2 * k], p[2 * k + 1]), out c);
                        if (c < 2) continue; // the node's own arm: its lanes are trimmed back to the ring
                    }
                    double half = 0.5 * Profiles[i].WidthAt(Math.Max(0, _along[i][k]));
                    best = Math.Min(best, d - half - 0.5);
                }
            }
            return Math.Max(0.0, best);
        }

        /// <summary>Widest game carriageway among motor pieces (not in <paramref name="exclude"/>) passing within
        /// <paramref name="radius"/> of a point.</summary>
        private float WidestNear(TileData t, double x, double z, double radius, List<int> exclude)
        {
            float best = 0f;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (exclude != null && exclude.Contains(i)) continue;
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    if (Sq(p[2 * k] / 100.0 - x) + Sq(p[2 * k + 1] / 100.0 - z) > radius * radius) continue;
                    best = Math.Max(best, Profiles[i].WidthAt(Math.Max(0, _along[i][k])));
                }
            }
            return best;
        }

        private int TopNear(TileData t, double x, double z, double radius)
        {
            int best = -1, prio = -1;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    if (Sq(p[2 * k] / 100.0 - x) + Sq(p[2 * k + 1] / 100.0 - z) > radius * radius) continue;
                    int pr = RoadStyle.Priority(r.RoadClass);
                    if (pr > prio)
                    {
                        prio = pr;
                        best = i;
                    }
                    break;
                }
            }
            return best;
        }

        /// <summary>
        /// A roundabout (W2_DESIGN 4.7): ring width = widest approach (game) + 1 m, at least 7 m, applied to the ring
        /// pieces; the island fills the inside of the ring (centreline diameter − ring width) unless mapped, with a
        /// 1 m mountable apron. Where the whole ring and its entries fit in the tile, it is drawn as a true circle
        /// (<see cref="RoadRing"/>); otherwise the ring ways are drawn as widened ribbons.
        /// </summary>
        private void AddRing(TileData t, double cx, double cz, double ringDiameter, double islandDiameter, List<int> ringRoads, bool police)
        {
            float approach = WidestNear(t, cx, cz, 0.5 * ringDiameter + 12.0, ringRoads);
            float ringWidth = Math.Max(7f, approach + 1f);
            double island = islandDiameter > 0 ? islandDiameter : ringDiameter - ringWidth;
            foreach (int i in ringRoads)
            {
                RoadWidthProfile p = Profiles[i];
                for (int k = 0; k < p.Count; k++)
                {
                    // Widen to the ring width where the corridor allows (never over a building).
                    p.Width[k] = Math.Max(p.Width[k], Math.Min(ringWidth, Math.Max(p.Limit[k], RoadWidthModel.RideableMinM(t.Roads[i].RoadClass))));
                    p.Drawn[k] = Math.Max(p.Drawn[k], p.Width[k]);
                    p.FootLeft[k] = p.FootRight[k] = 0f;
                }
            }
            int top = ringRoads.Count > 0 ? ringRoads[0] : TopNear(t, cx, cz, 0.5 * ringDiameter + 5);
            if (ringRoads.Count > 0 && TryTrueRing(t, cx, cz, 0.5 * ringDiameter, islandDiameter, ringWidth, ringRoads, police, top)) return;
            // The island (its 1 m apron is inside its radius) stays inside the ring carriageway's inner edge: a mapped
            // ring is rarely a circle (Maitighar's ring ways come within 22 m of a 34 m equal-area radius), and the raised
            // disc must never cover a ring lane.
            double inner = ringRoads.Count > 0 ? InnerRingRadius(t, cx, cz, ringRoads) : 0.5 * ringDiameter - 0.5 * ringWidth;
            island = Math.Min(island, 2.0 * (inner - 0.1));
            if (island >= 2.0)
                Islands.Add(new RoadIsland
                {
                    X = cx, Z = cz, RadiusM = (float)(0.5 * island), ApronM = 1.0f, Kind = IslandKind.Roundabout, PolicePodium = police, TopRoad = top,
                    PlantingM = PlantingFor(0.5 * island, 1.0f), Ring = -1,
                });
        }

        /// <summary>Nearest approach of the ring carriageways' inner edges (centreline distance minus the drawn half width)
        /// to (cx, cz).</summary>
        private double InnerRingRadius(TileData t, double cx, double cz, List<int> ringRoads)
        {
            double best = double.MaxValue;
            foreach (int i in ringRoads)
            {
                int[] p = t.Roads[i].Points;
                RoadWidthProfile prof = Profiles[i];
                double along = 0;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    double x = p[2 * k] / 100.0, z = p[2 * k + 1] / 100.0;
                    if (k > 0) along += Math.Sqrt(Sq(x - p[2 * k - 2] / 100.0) + Sq(z - p[2 * k - 1] / 100.0));
                    double d = Math.Sqrt(Sq(x - cx) + Sq(z - cz)) - 0.5 * prof.DrawnAt(along);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        /// <summary>Kerb-return radius of a ring entry flare for an approach of the given width.</summary>
        public static float FlareRadiusFor(float approachWidthM)
        {
            return approachWidthM >= 6.5f ? 6f : 4f;
        }

        private struct RingApproach
        {
            public int Road;
            public bool AtStart;
        }

        /// <summary>
        /// Lay out a true-circle ring: island radius from the mapping (or centreline radius − half the ring width), outer
        /// radius = island + ring width (at least centreline + half width). Approaches are the drawn street pieces with a
        /// free end on the ring (a node of a ring way, or within the ring band); footways and decks ending there are ignored
        /// (the ring surface covers them). Each approach is cut where its centreline is a flare radius beyond the outer
        /// circle (as far as the piece reaches), and both cut edges run onto the circle along cubic kerb-return flares,
        /// shrunk where neighbouring entries would overlap. False (and nothing recorded) when the ring would leave the tile,
        /// a street passes through it, an approach is too short to clear the circle, or the outline is not star-shaped.
        /// </summary>
        private bool TryTrueRing(TileData t, double cx, double cz, double centreR, double islandDiameter, float ringWidth, List<int> members, bool police, int top)
        {
            double islandR = islandDiameter > 0 ? 0.5 * islandDiameter : centreR - 0.5 * ringWidth;
            if (islandR < 2.0) return false;
            double outerR = Math.Max(centreR + 0.5 * ringWidth, islandR + ringWidth);
            double size = t.Tile.Size;
            var ringNodes = new HashSet<long>();
            foreach (int m in members)
            {
                int[] mp = t.Roads[m].Points;
                for (int k = 0; k < mp.Length / 2; k++) ringNodes.Add(Key(mp[2 * k], mp[2 * k + 1]));
            }
            var approaches = new List<RingApproach>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (members.Contains(i)) continue;
                RoadRecord r = t.Roads[i];
                if (!RoadMesher.IsDrawn(r, null) || (r.Flags & RoadFlags.Tunnel) != 0) continue;
                bool street = CapClass(r.RoadClass) && !Structures[i].IsElevated && (r.Flags & RoadFlags.Bridge) == 0 && r.Layer == 0;
                int first, last;
                Rendered(r, out first, out last);
                int[] p = r.Points;
                bool touches = false, inside = false;
                for (int k = first; k <= last; k++)
                {
                    double d = Math.Sqrt(Sq(p[2 * k] / 100.0 - cx) + Sq(p[2 * k + 1] / 100.0 - cz));
                    bool end = k == first && !r.HasPrevContext || k == last && !r.HasNextContext;
                    bool onRing = ringNodes.Contains(Key(p[2 * k], p[2 * k + 1])) ||
                                  d >= islandR - 1.0 && (Math.Abs(d - centreR) <= Math.Max(4.0, 0.3 * centreR) || d < outerR + 1.0);
                    if (end && onRing)
                    {
                        if (street) approaches.Add(new RingApproach { Road = i, AtStart = k == first });
                        touches = true;
                        continue;
                    }
                    if (d < outerR - 0.5) inside = true;
                }
                // A street crossing the ring area that is not an approach (a through carriageway) rules the circle out;
                // footways over the island and decks passing over do not.
                if (inside && !touches && street && !RoadWidthModel.IsFootClass(r.RoadClass)) return false;
            }
            if (approaches.Count == 0) return false;
            var arms = new List<RingArm>();
            foreach (RingApproach ap in approaches)
            {
                RoadRecord r = t.Roads[ap.Road];
                double len = Profiles[ap.Road].LengthM;
                float w = ap.AtStart ? Profiles[ap.Road].Drawn[0] : Profiles[ap.Road].Drawn[Profiles[ap.Road].Count - 1];
                float rho = FlareRadiusFor(w);
                // Walk away from the ring until the centreline is a flare radius out (or as far as the piece allows, at
                // least clear of the circle).
                double want = outerR + rho + 1.0, clear = outerR + 0.8;
                double s = double.NaN, fallback = double.NaN;
                for (double q = 0; q <= len - 0.5; q += 0.5)
                {
                    double sa = ap.AtStart ? q : len - q;
                    RoadCut c0 = DrawnCutAt(t, ap.Road, sa);
                    double d = Math.Sqrt(Sq(c0.CX - cx) + Sq(c0.CZ - cz));
                    if (d >= clear && double.IsNaN(fallback)) fallback = sa;
                    if (d >= want)
                    {
                        s = sa;
                        break;
                    }
                }
                if (double.IsNaN(s)) s = fallback;
                if (double.IsNaN(s)) return false;
                RoadCut cut = DrawnCutAt(t, ap.Road, s);
                double sgn = ap.AtStart ? 1 : -1;
                // Arm direction away from the ring: the piece direction at a start, reversed at an end.
                double dx = cut.UZ * sgn, dz = -cut.UX * sgn; // tangent = (UZ, -UX) for U = left normal of (tx, tz)
                double ang = Math.Atan2(cut.CZ - cz, cut.CX - cx);
                bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
                var arm = new RingArm
                {
                    Road = ap.Road, AtStart = ap.AtStart, Cut = cut, Angle = ang, DirX = dx, DirZ = dz, WidthM = w,
                    // Point order runs away from the ring at a start: a one-way approach starting at the ring is an exit.
                    Entry = !oneway || !ap.AtStart, Exit = !oneway || ap.AtStart,
                };
                if (!oneway && Profiles[ap.Road].AccessWidthAt(s) >= 7f && w >= 7f)
                {
                    // A teardrop between the entry and exit lanes, narrow enough that a 2 m car in each lane keeps 0.1 m
                    // clear of it (lanes centred at ±w/4 on a two-lane approach).
                    double width = Math.Min(2.4, Math.Min(0.22 * w, 0.5 * w - 2.2));
                    if (width >= 0.8)
                    {
                        arm.Splitter = true;
                        arm.SplitterLengthM = (float)Math.Max(6.0, Math.Min(15.0, Math.Min(1.4 * w, Math.Sqrt(Sq(cut.CX - cx) + Sq(cut.CZ - cz)) - outerR + 6.0)));
                        arm.SplitterWidthM = (float)width;
                    }
                }
                arms.Add(arm);
            }
            arms.Sort((a, b) => a.Angle.CompareTo(b.Angle));
            // Entry flares, shrunk (down to an eighth) where neighbouring entries would overlap; entries that still overlap
            // (the two carriageways of a dual approach) simply share the circle between them.
            int na = arms.Count;
            var scale = new double[na];
            for (int a = 0; a < na; a++) scale[a] = 1.0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                for (int a = 0; a < na; a++)
                {
                    RingArm arm = arms[a];
                    RoadCut c = arm.Cut;
                    double sgn = arm.AtStart ? 1 : -1;
                    // Arm-left (counter-clockwise side, seen from the centre looking out) is piece-left at a start.
                    arm.LeftEdgeX = c.CX + sgn * c.UX * (c.Half + sgn * c.Shift);
                    arm.LeftEdgeZ = c.CZ + sgn * c.UZ * (c.Half + sgn * c.Shift);
                    arm.RightEdgeX = c.CX - sgn * c.UX * (c.Half - sgn * c.Shift);
                    arm.RightEdgeZ = c.CZ - sgn * c.UZ * (c.Half - sgn * c.Shift);
                    double rho = FlareRadiusFor(arm.WidthM) * scale[a];
                    double[] right = Flare(cx, cz, outerR, arm.RightEdgeX, arm.RightEdgeZ, -arm.DirX, -arm.DirZ, rho, -1);
                    double[] left = Flare(cx, cz, outerR, arm.LeftEdgeX, arm.LeftEdgeZ, -arm.DirX, -arm.DirZ, rho, +1);
                    int nf = right.Length / 2;
                    arm.RightX = new double[nf];
                    arm.RightZ = new double[nf];
                    arm.LeftX = new double[nf];
                    arm.LeftZ = new double[nf];
                    for (int k = 0; k < nf; k++)
                    {
                        arm.RightX[k] = right[2 * k];
                        arm.RightZ[k] = right[2 * k + 1];
                        arm.LeftX[k] = left[2 * k];
                        arm.LeftZ[k] = left[2 * k + 1];
                    }
                    arm.AngleRight = Math.Atan2(arm.RightZ[0] - cz, arm.RightX[0] - cx);
                    arm.AngleLeft = Math.Atan2(arm.LeftZ[nf - 1] - cz, arm.LeftX[nf - 1] - cx);
                    arms[a] = arm;
                }
                bool overlap = false;
                for (int a = 0; a < na && na > 1; a++)
                {
                    int b = (a + 1) % na;
                    double spanA = Wrap(arms[a].AngleLeft - arms[a].AngleRight);
                    double gap = Wrap(arms[b].AngleRight - arms[a].AngleRight) - spanA;
                    if (spanA > Math.PI || gap >= 0.02) continue;
                    overlap = true;
                    scale[a] *= 0.5;
                    scale[b] *= 0.5;
                }
                if (!overlap) break;
            }
            // The ring and its entries stay inside the tile.
            if (cx - outerR < 0.5 || cz - outerR < 0.5 || cx + outerR > size - 0.5 || cz + outerR > size - 0.5) return false;
            foreach (RingArm arm in arms)
            {
                for (int k = 0; k < arm.RightX.Length; k++)
                    if (Outside(arm.RightX[k], arm.RightZ[k], size) || Outside(arm.LeftX[k], arm.LeftZ[k], size)) return false;
                if (Outside(arm.RightEdgeX, arm.RightEdgeZ, size) || Outside(arm.LeftEdgeX, arm.LeftEdgeZ, size)) return false;
            }
            var ring = new RoadRing
            {
                X = cx, Z = cz, InnerRadiusM = islandR, OuterRadiusM = outerR, WidthM = ringWidth, TopRoad = top, Members = members.ToArray(),
                Arms = arms.ToArray(), FlareRadiusM = FlareRadiusFor(ringWidth - 1f),
            };
            int index = Rings.Count;
            float apron = islandR >= 4.0 ? 1.0f : 0.5f;
            ring.Island = Islands.Count;
            Islands.Add(new RoadIsland
            {
                X = cx, Z = cz, RadiusM = (float)islandR, ApronM = apron, Kind = IslandKind.Roundabout, PolicePodium = police, TopRoad = top,
                PlantingM = PlantingFor(islandR, apron), Ring = index,
            });
            Rings.Add(ring);
            foreach (int m in members) RingOf[m] = index;
            foreach (RingArm arm in ring.Arms)
            {
                double len = Profiles[arm.Road].LengthM;
                RoadCut a0, a1;
                if (arm.AtStart)
                {
                    a0 = DrawnCutAt(t, arm.Road, 0);
                    a1 = arm.Cut;
                }
                else
                {
                    a0 = arm.Cut;
                    a1 = DrawnCutAt(t, arm.Road, len);
                }
                AddCutPair(arm.Road, a0, a1, top);
            }
            return true;
        }

        private static double Wrap(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

        private static bool Outside(double x, double z, double size)
        {
            return x < 0.5 || z < 0.5 || x > size - 0.5 || z > size - 0.5;
        }

        /// <summary>
        /// A ring-entry flare for one approach edge: a cubic curve from the edge point E, leaving along (dx, dz) (inward),
        /// to the circle point Q a flare radius round the circle from E's bearing (counter-clockwise for
        /// <paramref name="side"/> = +1, clockwise for −1), arriving along the circle. Returns xz pairs ordered
        /// counter-clockwise round the centre: the circle end first for the right edge, the edge end first for the left.
        /// The edge point itself is not included.
        /// </summary>
        private static double[] Flare(double cx, double cz, double R, double ex, double ez, double dx, double dz, double rho, int side)
        {
            double re = Math.Sqrt(Sq(ex - cx) + Sq(ez - cz));
            double thE = Math.Atan2(ez - cz, ex - cx);
            double thQ = thE + side * Math.Min(0.9, Math.Max(0.0, rho) / R);
            double qx = cx + R * Math.Cos(thQ), qz = cz + R * Math.Sin(thQ);
            // Travel direction at Q: along the circle, away from the entry.
            double tqx = -Math.Sin(thQ) * side, tqz = Math.Cos(thQ) * side;
            double reach = Math.Max(0.3, re - R);
            double a = 0.55 * Math.Max(reach, 0.5 * Math.Sqrt(Sq(qx - ex) + Sq(qz - ez)));
            double p1x = ex + dx * a, p1z = ez + dz * a, p2x = qx - tqx * a, p2z = qz - tqz * a;
            const int N = 6;
            var outp = new double[2 * N];
            for (int q = 1; q <= N; q++)
            {
                double u = (double)q / N, v = 1 - u;
                double px = v * v * v * ex + 3 * v * v * u * p1x + 3 * v * u * u * p2x + u * u * u * qx;
                double pz = v * v * v * ez + 3 * v * v * u * p1z + 3 * v * u * u * p2z + u * u * u * qz;
                // From the edge (q = 1) to the circle point (q = N); the right flare is listed circle first.
                int k = side < 0 ? N - q : q - 1;
                outp[2 * k] = px;
                outp[2 * k + 1] = pz;
            }
            return outp;
        }

        // -------------------------------------------------------------------------------------------------------
        // Junction caps
        // -------------------------------------------------------------------------------------------------------

        private struct Arm
        {
            public int Road, Point;
            public bool Forward;
            public double DX, DZ, Angle, SNode, Avail, OffL, OffR, Setback;
        }

        private void Junctions(TileData t)
        {
            double size = t.Tile.Size;
            // Rendered points of cap-class pieces by exact position.
            var nodes = new Dictionary<long, List<int>>();
            var order = new List<long>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0 || r.Layer != 0 || Structures[i].IsElevated ||
                    Structures[i].Kind == RoadStructureKind.Tunnel || RingOf[i] >= 0) continue;
                int first, last;
                Rendered(r, out first, out last);
                int[] p = r.Points;
                for (int k = first; k <= last; k++)
                {
                    long key = Key(p[2 * k], p[2 * k + 1]);
                    List<int> l;
                    if (!nodes.TryGetValue(key, out l))
                    {
                        nodes[key] = l = new List<int>(2);
                        order.Add(key);
                    }
                    l.Add(i << 16 | (k & 0xFFFF));
                }
            }
            // Junction nodes per road (for arm lengths).
            var nodeAlong = new List<double>[t.Roads.Count];
            var junctionKeys = new List<long>();
            foreach (long key in order)
            {
                List<int> l = nodes[key];
                if (l.Count < 2) continue;
                if (ArmCount(t, l) < 3) continue;
                junctionKeys.Add(key);
                foreach (int e in l)
                {
                    int ri = e >> 16, k = e & 0xFFFF;
                    if (nodeAlong[ri] == null) nodeAlong[ri] = new List<double>(2);
                    nodeAlong[ri].Add(_along[ri][k]);
                }
            }
            var cutLists = new List<CutEntry>[t.Roads.Count];
            for (int i = 0; i < t.Roads.Count; i++)
            {
                // Ring entries cut first; caps add to them.
                if (Cuts[i] == null) continue;
                cutLists[i] = new List<CutEntry>();
                for (int k = 0; k < Cuts[i].Length; k++) cutLists[i].Add(new CutEntry { Cut = Cuts[i][k], Top = CutTops[i][k] });
            }
            var arms = new List<Arm>(6);
            foreach (long key in junctionKeys)
            {
                int xcm = (int)(key >> 32), zcm = (int)(uint)key;
                double nx = xcm / 100.0, nz = zcm / 100.0;
                if (nx < CapBorderMarginM || nz < CapBorderMarginM || nx > size - CapBorderMarginM || nz > size - CapBorderMarginM) continue;
                if (InsideRing(nx, nz)) continue;
                arms.Clear();
                foreach (int e in nodes[key]) CollectArms(t, e >> 16, e & 0xFFFF, nodeAlong, arms);
                if (arms.Count < 3) continue;
                arms.Sort((a, b) => a.Angle.CompareTo(b.Angle));
                JunctionCap cap = BuildCap(t, nx, nz, arms);
                if (cap == null) continue;
                bool overlap = false;
                for (int k = 0; k < arms.Count && !overlap; k++)
                {
                    Arm a = arms[k];
                    double s0 = a.Forward ? a.SNode : a.SNode - a.Setback, s1 = a.Forward ? a.SNode + a.Setback : a.SNode;
                    // Never cut into another junction's gap or a ring entry.
                    List<CutEntry> l = cutLists[a.Road];
                    if (l == null) continue;
                    for (int g = 0; g + 1 < l.Count; g += 2)
                        if (s0 < l[g + 1].Cut.S && s1 > l[g].Cut.S) overlap = true;
                }
                if (overlap) continue;
                Caps.Add(cap);
                for (int k = 0; k < arms.Count; k++)
                {
                    Arm a = arms[k];
                    double s0 = a.Forward ? a.SNode : a.SNode - a.Setback, s1 = a.Forward ? a.SNode + a.Setback : a.SNode;
                    if (cutLists[a.Road] == null) cutLists[a.Road] = new List<CutEntry>();
                    cutLists[a.Road].Add(new CutEntry { Cut = DrawnCutAt(t, a.Road, s0), Top = cap.TopRoad });
                    cutLists[a.Road].Add(new CutEntry { Cut = DrawnCutAt(t, a.Road, s1), Top = cap.TopRoad });
                }
            }
            for (int i = 0; i < cutLists.Length; i++)
            {
                if (cutLists[i] == null) continue;
                List<CutEntry> l = cutLists[i];
                // Pairs are (start, end); sort pairs by start and merge overlapping ones.
                var pairs = new List<CutEntry[]>();
                for (int k = 0; k + 1 < l.Count; k += 2) pairs.Add(new[] { l[k], l[k + 1] });
                pairs.Sort((a, b) => a[0].Cut.S.CompareTo(b[0].Cut.S));
                var merged = new List<CutEntry>();
                foreach (CutEntry[] pr in pairs)
                {
                    if (merged.Count > 0 && pr[0].Cut.S <= merged[merged.Count - 1].Cut.S)
                    {
                        if (pr[1].Cut.S > merged[merged.Count - 1].Cut.S) merged[merged.Count - 1] = pr[1];
                        continue;
                    }
                    merged.Add(pr[0]);
                    merged.Add(pr[1]);
                }
                Cuts[i] = new RoadCut[merged.Count];
                CutTops[i] = new int[merged.Count];
                for (int k = 0; k < merged.Count; k++)
                {
                    Cuts[i][k] = merged[k].Cut;
                    CutTops[i][k] = merged[k].Top;
                }
            }
            MatchJunctionRecords(t);
        }

        private void AddCutPair(int road, RoadCut a, RoadCut b, int top)
        {
            RoadCut[] old = Cuts[road];
            int n = old == null ? 0 : old.Length;
            var c = new RoadCut[n + 2];
            var tp = new int[n + 2];
            if (old != null)
            {
                Array.Copy(old, c, n);
                Array.Copy(CutTops[road], tp, n);
            }
            c[n] = a;
            c[n + 1] = b;
            tp[n] = tp[n + 1] = top;
            Cuts[road] = c;
            CutTops[road] = tp;
        }

        private struct CutEntry
        {
            public RoadCut Cut;
            public int Top;
        }

        private bool InsideRing(double x, double z)
        {
            foreach (RoadRing g in Rings)
            {
                double r = g.OuterRadiusM + g.FlareRadiusM + 2.0;
                if (Sq(x - g.X) + Sq(z - g.Z) < r * r) return true;
            }
            return false;
        }

        private int ArmCount(TileData t, List<int> entries)
        {
            int n = 0;
            foreach (int e in entries)
            {
                RoadRecord r = t.Roads[e >> 16];
                int k = e & 0xFFFF;
                int first, last;
                Rendered(r, out first, out last);
                if (k > first) n++;
                if (k < last) n++;
            }
            return n;
        }

        private void CollectArms(TileData t, int ri, int k, List<double>[] nodeAlong, List<Arm> arms)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int first, last;
            Rendered(r, out first, out last);
            double s = _along[ri][k];
            double length = Profiles[ri].LengthM;
            for (int dir = 0; dir < 2; dir++)
            {
                bool forward = dir == 0;
                if (forward ? k >= last : k <= first) continue;
                int step = forward ? 1 : -1, q = k + step;
                // Direction to the next point at least 0.3 m away.
                while (q > first && q < last && Sq((p[2 * q] - p[2 * k]) / 100.0) + Sq((p[2 * q + 1] - p[2 * k + 1]) / 100.0) < 0.09) q += step;
                double dx = (p[2 * q] - p[2 * k]) / 100.0, dz = (p[2 * q + 1] - p[2 * k + 1]) / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) continue;
                dx /= l;
                dz /= l;
                // Room along the arm: to the next junction node (shared) or the piece end.
                double room = forward ? length - s : s;
                bool shared = false;
                if (nodeAlong[ri] != null)
                    foreach (double o in nodeAlong[ri])
                    {
                        double d = forward ? o - s : s - o;
                        if (d > 1e-6 && d < room)
                        {
                            room = d;
                            shared = true;
                        }
                    }
                // Room up to the first corner of the arm: the cap's straight edges assume the arm runs straight out.
                double corner = room;
                for (int c = k + step; c > first && c < last; c += step)
                {
                    double ex = (p[2 * (c + step)] - p[2 * c]) / 100.0, ez = (p[2 * (c + step) + 1] - p[2 * c + 1]) / 100.0;
                    double el = Math.Sqrt(ex * ex + ez * ez);
                    if (el < 1e-6) continue;
                    if ((ex * dx + ez * dz) / el < Math.Cos(25.0 * Math.PI / 180.0))
                    {
                        corner = Math.Abs(_along[ri][c] - s);
                        break;
                    }
                }
                RoadProfile pr = RoadWidthModel.ProfileFrom(r, Attrs[ri], Profiles[ri], s);
                float half = 0.5f * pr.DrawnM;
                double shift = pr.DrawnShiftM;
                arms.Add(new Arm
                {
                    Road = ri, Point = k, Forward = forward, DX = dx, DZ = dz, Angle = Math.Atan2(dz, dx), SNode = s,
                    Avail = Math.Min(MaxSetbackM, Math.Min(room * (shared ? 0.45 : 0.9), Math.Max(corner, 2.0))),
                    OffL = forward ? half + shift : half - shift, OffR = forward ? half - shift : half + shift,
                });
            }
        }

        /// <summary>Kerb-return radius between two arms (W2_DESIGN 4.7): 6 m where both are at least 6.5 m wide, 3 m
        /// otherwise, 1.5 m in old cores (tight brick corners, still rounded).</summary>
        public static double KerbReturnRadiusM(float widthA, float widthB, bool oldCore)
        {
            if (oldCore) return 1.5;
            return widthA >= 6.5f && widthB >= 6.5f ? 6.0 : 3.0;
        }

        /// <summary>Largest angle step of a kerb-return arc.</summary>
        public const double KerbArcStepDeg = 15.0;

        private JunctionCap BuildCap(TileData t, double nx, double nz, List<Arm> arms)
        {
            int n = arms.Count;
            float widest = 0f;
            int top = arms[0].Road;
            bool oldCore = false;
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k];
                widest = Math.Max(widest, (float)(a.OffL + a.OffR));
                if (RoadStyle.Priority(t.Roads[a.Road].RoadClass) > RoadStyle.Priority(t.Roads[top].RoadClass)) top = a.Road;
                if (RoadWidthModel.AreaOf(Attrs[a.Road]) == AreaType.OldCore) oldCore = true;
                a.Setback = 0.5;
                arms[k] = a;
            }
            var px = new double[n];
            var pz = new double[n];
            var ta = new double[n];
            var tb = new double[n];
            var tan = new double[n];
            var theta = new double[n];
            var hasP = new bool[n];
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k], b = arms[(k + 1) % n];
                double th = b.Angle - a.Angle;
                if (th <= 0) th += 2 * Math.PI;
                if (n == 1 || th > 170.0 * Math.PI / 180.0 || th < 1.0 * Math.PI / 180.0) continue;
                // Left edge of a: N + nA·offL + ta·dA ; right edge of b: N − nB·offR + tb·dB ; n = (−dz, dx).
                double ax = a.DX, az = a.DZ, bx = -b.DX, bz = -b.DZ;
                double cx = -(-b.DZ) * b.OffR - (-a.DZ) * a.OffL, cz = -(b.DX) * b.OffR - (a.DX) * a.OffL;
                double den = ax * bz - az * bx;
                if (Math.Abs(den) < 1e-9) continue;
                double tA = (cx * bz - cz * bx) / den, tB = (ax * cz - az * cx) / den;
                if (tA < 0) tA = 0;
                if (tB < 0) tB = 0;
                double rho = KerbReturnRadiusM((float)(a.OffL + a.OffR), (float)(b.OffL + b.OffR), oldCore);
                double T = rho / Math.Tan(0.5 * th);
                a.Setback = Math.Max(a.Setback, tA + T + 0.2);
                b.Setback = Math.Max(b.Setback, tB + T + 0.2);
                arms[k] = a;
                arms[(k + 1) % n] = b;
                px[k] = nx + a.DX * tA - a.DZ * a.OffL;
                pz[k] = nz + a.DZ * tA + a.DX * a.OffL;
                ta[k] = tA;
                tb[k] = tB;
                tan[k] = T;
                theta[k] = th;
                hasP[k] = true;
            }
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k];
                a.Setback = Math.Min(a.Setback, a.Avail);
                if (a.Setback < 0.5) return null; // no room: leave this node to plain overlapping ribbons
                arms[k] = a;
            }
            var xs = new List<double>(n * 12);
            var zs = new List<double>(n * 12);
            var rightIdx = new int[n];
            var leftIdx = new int[n];
            var radii = new float[n];
            var cuts = new RoadCut[n];
            for (int k = 0; k < n; k++) cuts[k] = DrawnCutAt(t, arms[k].Road, arms[k].Forward ? arms[k].SNode + arms[k].Setback : arms[k].SNode - arms[k].Setback);
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k], b = arms[(k + 1) % n];
                RoadCut ca = cuts[k];
                double sgn = a.Forward ? 1 : -1;
                // Arm-left = piece-left for a forward arm.
                double lx = ca.CX + sgn * ca.UX * (ca.Half + sgn * ca.Shift), lz = ca.CZ + sgn * ca.UZ * (ca.Half + sgn * ca.Shift);
                double rx = ca.CX - sgn * ca.UX * (ca.Half - sgn * ca.Shift), rz = ca.CZ - sgn * ca.UZ * (ca.Half - sgn * ca.Shift);
                rightIdx[k] = xs.Count;
                xs.Add(rx);
                zs.Add(rz);
                leftIdx[k] = xs.Count;
                xs.Add(lx);
                zs.Add(lz);
                if (!hasP[k] || n < 2) continue;
                // Kerb return: the circle tangent to a's left edge and b's right edge, as large as both setbacks allow.
                double T = Math.Min(tan[k], Math.Min(a.Setback - ta[k], b.Setback - tb[k]) - 0.05);
                if (T < 0.05) continue;
                double th = theta[k];
                double rho = T * Math.Tan(0.5 * th);
                radii[k] = (float)rho;
                double tAx = px[k] + a.DX * T, tAz = pz[k] + a.DZ * T;
                double tBx = px[k] + b.DX * T, tBz = pz[k] + b.DZ * T;
                double bisx = a.DX + b.DX, bisz = a.DZ + b.DZ, bl = Math.Sqrt(bisx * bisx + bisz * bisz);
                if (bl < 1e-9) continue;
                double ccx = px[k] + bisx / bl * rho / Math.Sin(0.5 * th), ccz = pz[k] + bisz / bl * rho / Math.Sin(0.5 * th);
                if (Sq(tAx - lx) + Sq(tAz - lz) > 1e-4)
                {
                    xs.Add(tAx);
                    zs.Add(tAz);
                }
                double a0 = Math.Atan2(tAz - ccz, tAx - ccx), a1 = Math.Atan2(tBz - ccz, tBx - ccx);
                double sweep = a1 - a0;
                while (sweep > Math.PI) sweep -= 2 * Math.PI;
                while (sweep < -Math.PI) sweep += 2 * Math.PI;
                int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (KerbArcStepDeg * Math.PI / 180.0)));
                for (int q = 1; q < steps; q++)
                {
                    double ang = a0 + sweep * q / steps;
                    xs.Add(ccx + rho * Math.Cos(ang));
                    zs.Add(ccz + rho * Math.Sin(ang));
                }
                RoadCut cb = cuts[(k + 1) % n];
                double sb = b.Forward ? 1 : -1;
                double brx = cb.CX - sb * cb.UX * (cb.Half - sb * cb.Shift), brz = cb.CZ - sb * cb.UZ * (cb.Half - sb * cb.Shift);
                if (Sq(tBx - brx) + Sq(tBz - brz) > 1e-4)
                {
                    xs.Add(tBx);
                    zs.Add(tBz);
                }
            }
            double size = t.Tile.Size;
            for (int k = 0; k < xs.Count; k++)
                if (xs[k] < 0.05 || zs[k] < 0.05 || xs[k] > size - 0.05 || zs[k] > size - 0.05) return null; // the cap would leave the tile
            var cap = new JunctionCap
            {
                X = nx, Z = nz, PolyX = xs.ToArray(), PolyZ = zs.ToArray(), Count = xs.Count, ArmRoads = new int[n],
                ArmForward = new bool[n], ArmSetback = new double[n], ArmCutS = new double[n], TopRoad = top, WidestArmM = widest,
                ArmRightIndex = rightIdx, ArmLeftIndex = leftIdx, CornerRadiusM = radii,
            };
            for (int k = 0; k < n; k++)
            {
                cap.ArmRoads[k] = arms[k].Road;
                cap.ArmForward[k] = arms[k].Forward;
                cap.ArmSetback[k] = arms[k].Setback;
                cap.ArmCutS[k] = cuts[k].S;
            }
            return cap;
        }

        /// <summary>The section of road <paramref name="ri"/> at raw along <paramref name="s"/>: the point on the smoothed
        /// centreline (the mapped polyline while the layout is being built), the left normal there, and the carriageway
        /// (<see cref="HalfWidthAt"/>, its shift <see cref="RoadWidthProfile.ShiftAt"/>).</summary>
        public RoadCut CutAt(TileData t, int ri, double s)
        {
            return Section(t, ri, s, false);
        }

        /// <summary>The stored cut of road <paramref name="ri"/> at raw along <paramref name="s"/> (a junction cap's or ring
        /// entry's, exactly as the ribbon's end row uses it: laid out on the mapped polyline while the layout was built), or
        /// <see cref="DrawnCutAt"/> when there is none there. Caps insert the ribbon's columns along it, so cap and ribbon
        /// share their cut edge vertex for vertex.</summary>
        public RoadCut StoredCut(TileData t, int ri, double s)
        {
            RoadCut[] cuts = Cuts[ri];
            if (cuts != null)
                for (int k = 0; k < cuts.Length; k++)
                    if (Math.Abs(cuts[k].S - s) <= 1e-6) return cuts[k];
            return DrawnCutAt(t, ri, s);
        }

        /// <summary>As <see cref="CutAt"/> with the drawn carriageway (<see cref="DrawnHalfWidthAt"/>): where the ribbon's
        /// edges are.</summary>
        public RoadCut DrawnCutAt(TileData t, int ri, double s)
        {
            return Section(t, ri, s, true);
        }

        private RoadCut Section(TileData t, int ri, double s, bool drawn)
        {
            RoadRecord r = t.Roads[ri];
            RoadProfile pr = RoadWidthModel.ProfileFrom(r, Attrs[ri], Profiles[ri], s);
            double half = 0.5 * (drawn ? pr.DrawnM : pr.CarriagewayM), shift = drawn ? pr.DrawnShiftM : pr.CentreShiftM;
            RoadCentreline c = Centres[ri];
            if (c != null && c.Count >= 2)
            {
                double x, z, tx, tz;
                c.At(s, out x, out z, out tx, out tz);
                return new RoadCut { S = s, CX = x, CZ = z, UX = -tz, UZ = tx, Half = half, Shift = shift };
            }
            int[] p = r.Points;
            double[] al = _along[ri];
            int first, last;
            Rendered(r, out first, out last);
            int seg = first;
            for (int k = first; k < last; k++)
            {
                seg = k;
                if (al[k + 1] >= s) break;
            }
            double ax = p[2 * seg] / 100.0, az = p[2 * seg + 1] / 100.0, bx = p[2 * seg + 2] / 100.0, bz = p[2 * seg + 3] / 100.0;
            double len = al[seg + 1] - al[seg];
            double f = len > 1e-9 ? (s - al[seg]) / len : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                dx = 1;
                dz = 0;
                l = 1;
            }
            return new RoadCut
            {
                S = s, CX = ax + (bx - ax) * f, CZ = az + (bz - az) * f, UX = -dz / l, UZ = dx / l,
                Half = half, Shift = shift,
            };
        }

        private void MatchJunctionRecords(TileData t)
        {
            foreach (JunctionCap c in Caps)
            {
                foreach (JunctionRecord j in t.Junctions)
                {
                    if (Sq(j.XCm / 100.0 - c.X) + Sq(j.ZCm / 100.0 - c.Z) > 9.0) continue;
                    c.Kind = j.Kind;
                    c.Flags = j.Flags;
                    break;
                }
            }
        }
    }
}
