using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Routing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #4 (docs/W2_PLAYTEST_1.md): "Buildings constantly flickers and stutters... Make them stable and
    /// solid." A headless ride through the old core on the real sample region: a rider at 10 m/s from Thamel Chowk to
    /// Asan along the streets a scooter takes (A* on the sample GHRG: Dabali Marga, Amrit Marg, Thahiti, Narsingh Chowk,
    /// Thahity Jyatha Marg, about 1.1 km), stopping three times, with a chase camera that sways behind the rider while
    /// riding and swings around it while stopped. Every frame, every house near the B0 band is checked against what the
    /// bands draw: B0 cells (the streamer's cell set) and the B1 block standing in for them, with the shader's dithered
    /// cross-fades (BandConfig.Opacity, the C# twin of GhBandClip):
    /// <list type="bullet">
    /// <item><b>gap</b>: a part of a house the bands should draw has pixels no band draws (a hole, or a house drawn only by
    /// half a dither);</item>
    /// <item><b>double</b>: a house drawn twice (two B0 meshes, or a B0 mesh and the B1 block standing in for it);</item>
    /// <item><b>block flip</b>: a B1 block switching between standing in and handing over to B0 (the whole 128 m block
    /// pops between boxes and houses);</item>
    /// <item><b>re-show</b>: a cell shown again after it was hidden;</item>
    /// <item><b>level reversal</b>: a house switching level and back within a few seconds;</item>
    /// <item><b>churn</b>: cells rebuilt while the rider stands still;</item>
    /// <item><b>colliders</b>: a cell's registered structure colliders differing from what it draws.</item>
    /// </list>
    /// The planned hand-overs are not counted: the B0/B1 cross-fade at the band radius, a house changing level once on
    /// its way in or out, and the load at the start (the bench settles before the ride). The old ring cells (main at
    /// 5a7668a: near and lite rings, 24 m margins, whole blocks standing in while any wanted cell builds) are emulated
    /// with the same worker cost model to show the flicker the owner saw; the cell set must show none of it.
    /// </summary>
    public class MeshingBuildingB0StabilityTests
    {
        private static readonly string[] TierName = { "Low", "Mid", "High" };

        // ------------------------------------------------------------------------------------------------------------
        // Worker cost model

        /// <summary>
        /// Worker milliseconds per house built at the near, lite and flat level: the Debug desktop build on the shared
        /// four-core test machine (BuildingDetailMesher.One on the 274 houses within 100 m of Asan: 22 to 25, 4.4 to 6.4 and
        /// 2.8 to 4.7 ms a house over the tiers), about what a mid-range phone's release build takes. Deterministic, so the
        /// ride gives the same frames on every machine.
        /// </summary>
        internal static readonly double[] BuildMs = { 24.0, 5.0, 3.5 };

        /// <summary>Worker milliseconds per house a rebuild copies from the cell's previous version (B0CellSet.CopyHouse on
        /// the 1,805 houses of the High cells at Asan: 0.12 ms, same build).</summary>
        internal const double CopyMs = 0.12;

        internal const double FrameMs = 1000.0 / 30.0;

        // ------------------------------------------------------------------------------------------------------------
        // The street

        /// <summary>A polyline in game metres with its arc length.</summary>
        internal sealed class Street
        {
            public readonly List<double> X = new List<double>(), Z = new List<double>(), S = new List<double>();
            public string Names = "";

            public double Length
            {
                get { return S.Count > 0 ? S[S.Count - 1] : 0; }
            }

            public void Add(double x, double z)
            {
                if (X.Count > 0)
                {
                    double dx = x - X[X.Count - 1], dz = z - Z[Z.Count - 1], d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 0.05) return;
                    S.Add(S[S.Count - 1] + d);
                }
                else S.Add(0);
                X.Add(x);
                Z.Add(z);
            }

            public void At(double s, out double x, out double z)
            {
                s = Math.Max(0, Math.Min(Length, s));
                int lo = 0, hi = S.Count - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) / 2;
                    if (S[mid] <= s) lo = mid;
                    else hi = mid;
                }
                double seg = S[hi] - S[lo], t = seg > 1e-9 ? (s - S[lo]) / seg : 0;
                x = X[lo] + (X[hi] - X[lo]) * t;
                z = Z[lo] + (Z[hi] - Z[lo]) * t;
            }

            /// <summary>Unit heading at <paramref name="s"/> (over 6 m of street, as a rider's line).</summary>
            public void Heading(double s, out double hx, out double hz)
            {
                double ax, az, bx, bz;
                At(s - 3, out ax, out az);
                At(s + 3, out bx, out bz);
                double dx = bx - ax, dz = bz - az, d = Math.Sqrt(dx * dx + dz * dz);
                hx = d > 1e-6 ? dx / d : 0;
                hz = d > 1e-6 ? dz / d : 1;
            }
        }

        private static readonly Dictionary<string, Street> Routes = new Dictionary<string, Street>();
        private static RouteGraph _graph;

        /// <summary>The scooter's route from Thamel Chowk to Asan on the sample routing graph (A*, motorbike profile).</summary>
        internal static Street ThamelToAsan()
        {
            return RouteOf("Thamel Chowk to Asan", 85.3110, 27.7150, 85.3122, 27.7074, Travel.Motorbike);
        }

        /// <summary>A car's route from Lainchaur down Kantipath and Durbar Marg to Maitighar Mandala (A*, car profile): the
        /// main roads, driven at 50 km/h.</summary>
        internal static Street LainchaurToMaitighar()
        {
            return RouteOf("Lainchaur to Maitighar", 85.3140, 27.7185, 85.3220, 27.6945, Travel.Car);
        }

        /// <summary>The route between two places on the sample routing graph (A*, <paramref name="travel"/>), cached.</summary>
        internal static Street RouteOf(string name, double lon0, double lat0, double lon1, double lat1, Travel travel)
        {
            lock (Routes)
            {
                Street st;
                if (Routes.TryGetValue(name, out st)) return st;
                if (_graph == null)
                {
                    string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.route.ghrg");
                    _graph = RouteGraphReader.Read(File.ReadAllBytes(path));
                }
                RouteGraph g = _graph;
                double tx, tz, ax, az;
                WorldFrame.LonLatToGame(lon0, lat0, out tx, out tz);
                WorldFrame.LonLatToGame(lon1, lat1, out ax, out az);
                int src = new NearestNode(g, travel, false).Find(tx, tz);
                int dst = new NearestNode(g, travel, true).Find(ax, az);
                Route r = new AStar(g).FindRoute(src, dst, travel);
                Assert.IsNotNull(r, "a " + travel + " route " + name);
                st = new Street();
                var names = new List<string>();
                foreach (int e in r.Edges)
                {
                    double[] p = g.EdgeGeometry(e);
                    for (int i = 0; i < p.Length / 2; i++) st.Add(p[2 * i], p[2 * i + 1]);
                    NameRecord n = g.EdgeNameRecord(e);
                    string label = n != null ? (n.En.Length > 0 ? n.En : n.Default) : "";
                    if (label.Length > 0 && (names.Count == 0 || names[names.Count - 1] != label)) names.Add(label);
                }
                st.Names = string.Join(", ", names);
                Routes[name] = st;
                return st;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // The ride

        internal struct RideFrame
        {
            public double T, RiderX, RiderZ, CamX, CamZ;
            public bool Stopped;
            public int Stop;
        }

        /// <summary>
        /// Frames of a ride along <paramref name="st"/> at <paramref name="speed"/> m/s and 30 fps, stopping 5 s at 35%
        /// and 70% of the way and at the end. The chase camera rides 4.5 m behind the rider, swaying sideways (0.8 m at
        /// 0.4 Hz and 0.25 m at 2.7 Hz) and along the boom (0.3 m at 1.1 Hz); while stopped it swings ±40° around the
        /// rider.
        /// </summary>
        internal static List<RideFrame> Ride(Street st, double speed)
        {
            var frames = new List<RideFrame>();
            double dt = FrameMs / 1000.0, s = 0, t = 0;
            double[] stops = { 0.35 * st.Length, 0.7 * st.Length, st.Length };
            int next = 0;
            double stoppedFor = -1;
            while (true)
            {
                bool stopped = stoppedFor >= 0;
                double rx, rz, hx, hz;
                st.At(s, out rx, out rz);
                st.Heading(s, out hx, out hz);
                double nx = hz, nz = -hx, cx, cz;
                if (!stopped)
                {
                    double side = 0.8 * Math.Sin(2 * Math.PI * 0.4 * t) + 0.25 * Math.Sin(2 * Math.PI * 2.7 * t);
                    double boom = 4.5 + 0.3 * Math.Sin(2 * Math.PI * 1.1 * t);
                    cx = rx - hx * boom + nx * side;
                    cz = rz - hz * boom + nz * side;
                }
                else
                {
                    double a = 0.7 * Math.Sin(2 * Math.PI * 0.15 * stoppedFor), ca = Math.Cos(a), sa = Math.Sin(a);
                    double bx = hx * ca - hz * sa, bz = hx * sa + hz * ca;
                    cx = rx - bx * 4.5 + nx * 0.1 * Math.Sin(2 * Math.PI * 2.7 * t);
                    cz = rz - bz * 4.5 + nz * 0.1 * Math.Sin(2 * Math.PI * 2.7 * t);
                }
                frames.Add(new RideFrame { T = t, RiderX = rx, RiderZ = rz, CamX = cx, CamZ = cz, Stopped = stopped, Stop = stopped ? next : -1 });
                t += dt;
                if (stopped)
                {
                    stoppedFor += dt;
                    if (stoppedFor >= 5.0)
                    {
                        stoppedFor = -1;
                        if (++next >= stops.Length) break;
                    }
                    continue;
                }
                s += speed * dt;
                if (next < stops.Length && s >= stops[next])
                {
                    s = stops[next];
                    stoppedFor = 0;
                }
            }
            return frames;
        }

        /// <summary>
        /// Frames of a rider dithering on one stretch of street: from <paramref name="s0"/> it rides
        /// <paramref name="length"/> m on at <paramref name="speed"/> m/s, turns, rides back, and so on for
        /// <paramref name="legs"/> legs, standing 1.5 s at each turn while the chase camera swings round behind it (a rider
        /// looking for a lane, or the owner testing the flicker by rolling to and fro). The camera sways as in
        /// <see cref="Ride"/>.
        /// </summary>
        internal static List<RideFrame> Shuttle(Street st, double s0, double length, double speed, int legs)
        {
            var frames = new List<RideFrame>();
            double dt = FrameMs / 1000.0, t = 0, s = s0;
            for (int leg = 0; leg < legs; leg++)
            {
                double dir = leg % 2 == 0 ? 1 : -1, end = s0 + (leg % 2 == 0 ? length : 0);
                for (double turn = 0; turn < 1.5; turn += dt)
                {
                    double rx, rz, hx, hz;
                    st.At(s, out rx, out rz);
                    st.Heading(s, out hx, out hz);
                    hx *= dir;
                    hz *= dir;
                    // The boom swings from the old heading round to the new one over the stand.
                    double a = Math.PI * (1 - Math.Min(1, turn / 1.2)), ca = Math.Cos(a), sa = Math.Sin(a);
                    double bx = leg == 0 ? hx : hx * ca - hz * sa, bz = leg == 0 ? hz : hx * sa + hz * ca;
                    frames.Add(new RideFrame { T = t, RiderX = rx, RiderZ = rz, CamX = rx - bx * 4.5, CamZ = rz - bz * 4.5, Stopped = true, Stop = leg });
                    t += dt;
                }
                while (dir > 0 ? s < end : s > end)
                {
                    s = dir > 0 ? Math.Min(end, s + speed * dt) : Math.Max(end, s - speed * dt);
                    double rx, rz, hx, hz;
                    st.At(s, out rx, out rz);
                    st.Heading(s, out hx, out hz);
                    hx *= dir;
                    hz *= dir;
                    double nx = hz, nz = -hx;
                    double side = 0.8 * Math.Sin(2 * Math.PI * 0.4 * t) + 0.25 * Math.Sin(2 * Math.PI * 2.7 * t);
                    double boom = 4.5 + 0.3 * Math.Sin(2 * Math.PI * 1.1 * t);
                    frames.Add(new RideFrame { T = t, RiderX = rx, RiderZ = rz, CamX = rx - hx * boom + nx * side, CamZ = rz - hz * boom + nz * side, Stop = -1 });
                    t += dt;
                }
            }
            return frames;
        }

        // ------------------------------------------------------------------------------------------------------------
        // What the bands draw

        /// <summary>The counters of one ride.</summary>
        internal sealed class Tally
        {
            public int Frames, GapFrames, DoubleFrames, BlockFlips, Reshows, QuickReshows, LevelReversals, StopBuilds, ColliderMismatches, Handovers;
            public long GapParts, DoubleParts;
            public int PeakShownTris, PeakMovingTris, PeakInViewTris, BlocksStandingIn;
            public long MostUploadBytes;
            public readonly HashSet<long> GapHouses = new HashSet<long>(), DoubleHouses = new HashSet<long>();
            public string FirstGap, FirstDouble, FirstFlip, PeakNote, FirstReversal, FirstReshow, Work;

            /// <summary>Per frame, what showed: 1 a gap, 2 a double, 4 a block flip, 8 a cell re-show (the ride timeline
            /// preview, <see cref="DumpRideTimelines"/>).</summary>
            public readonly List<byte> Timeline = new List<byte>();

            /// <summary>Records one frame's events in the <see cref="Timeline"/> (counts before the frame given).</summary>
            public void Mark(bool gap, bool dbl, int flipsBefore, int reshowsBefore)
            {
                Timeline.Add((byte)((gap ? 1 : 0) | (dbl ? 2 : 0) | (BlockFlips > flipsBefore ? 4 : 0) | (Reshows > reshowsBefore ? 8 : 0)));
            }

            public override string ToString()
            {
                return string.Format("{0} frames: gaps in {1} frames ({2} houses), doubles in {3} frames ({4} houses), {5} block flips, {6} cell re-shows " +
                                     "({7} within 2 s), {8} level reversals, {9} rebuilds while stopped, {10} collider mismatches; {11} hand-over parts; " +
                                     "peak {12} B0 tris ({13} riding, {14} in the camera's 70 degree view)", Frames, GapFrames, GapHouses.Count, DoubleFrames, DoubleHouses.Count, BlockFlips, Reshows, QuickReshows,
                                     LevelReversals, StopBuilds, ColliderMismatches, Handovers, PeakShownTris, PeakMovingTris, PeakInViewTris);
            }
        }

        /// <summary>A house's B0 drawers: up to two B0 meshes with their band ranges (the old near and lite rings), and
        /// whether the B1 block of each part stands in.</summary>
        private struct Drawers
        {
            public int Count;
            public float Inner0, Outer0, Inner1, Outer1;

            public void Add(float inner, float outer)
            {
                if (Count == 0)
                {
                    Inner0 = inner;
                    Outer0 = outer;
                }
                else
                {
                    Inner1 = inner;
                    Outer1 = outer;
                }
                Count++;
            }
        }

        /// <summary>
        /// Coverage of one part of a house (horizontal distances [<paramref name="dmin"/>, <paramref name="dmax"/>] from
        /// the band centre) by its B0 drawers and its B1 block (standing in: <paramref name="b1Full"/>), with the shader's
        /// dithered ramps: a gap where the opacities sum below 1, a double where two meshes of the house draw the same
        /// distance (two B0 meshes, or B0 and the standing-in block). A part in the B0/B1 cross-fade drawn by B0 and the
        /// handing-over block is the planned hand-over.
        /// </summary>
        private static void Cover(double dmin, double dmax, in Drawers d, bool b1Full, BandConfig bands, out bool gap, out bool dbl, out bool handover)
        {
            gap = dbl = handover = false;
            double h = BandConfig.FadeM * 0.5, lo = Math.Max(0, dmin), hi = Math.Min(dmax, bands.B0OuterM + h);
            if (lo > hi) return;
            // Opacities only vary inside the fades: sample both ends and every 0.25 m of each fade the part spans.
            double[] edges = { d.Count > 0 ? d.Outer0 : -100, d.Count > 1 ? d.Inner1 : -100, bands.B0OuterM };
            Check(lo, d, b1Full, bands, ref gap, ref dbl, ref handover);
            Check(hi, d, b1Full, bands, ref gap, ref dbl, ref handover);
            foreach (double e in edges)
            {
                if (e < 0) continue;
                for (double x = Math.Max(lo, e - h); x <= Math.Min(hi, e + h); x += 0.25) Check(x, d, b1Full, bands, ref gap, ref dbl, ref handover);
            }
        }

        private static void Check(double x, in Drawers d, bool b1Full, BandConfig bands, ref bool gap, ref bool dbl, ref bool handover)
        {
            double sum = 0;
            int meshes = 0;
            if (d.Count > 0)
            {
                float o = BandConfig.Opacity(x, d.Inner0, d.Outer0);
                sum += o;
                if (o > 0) meshes++;
            }
            if (d.Count > 1)
            {
                float o = BandConfig.Opacity(x, d.Inner1, d.Outer1);
                sum += o;
                if (o > 0) meshes++;
            }
            float b1 = BandConfig.Opacity(x, b1Full ? 0f : bands.B0OuterM, bands.B1OuterM);
            sum += b1;
            if (sum < 0.999) gap = true;
            if (meshes >= 2 || (meshes >= 1 && b1Full && b1 > 0)) dbl = true;
            if (meshes == 1 && !b1Full && b1 > 0) handover = true;
        }

        /// <summary>Per tile: whether a house makes B0 geometry at all (heroes, dropped and part-only outlines do not; neither
        /// band draws those), built once at the flat level when first asked.</summary>
        private sealed class Drawable
        {
            private readonly Dictionary<long, bool> _known = new Dictionary<long, bool>();
            private readonly BuildingOptions _flat;
            private readonly MeshData _m = new MeshData();

            public Drawable(BandConfig bands, BuildingOptions shared)
            {
                _flat = bands.B0Options(BandConfig.LevelFlat, shared);
            }

            public bool Of(TileId id, TileData t, IHeightSampler h, int i)
            {
                long key = Key(id, i);
                bool r;
                if (_known.TryGetValue(key, out r)) return r;
                _m.Clear();
                r = BuildingDetailMesher.One(t, i, h, _flat, _m, null) && _m.TriangleCount > 0;
                _known[key] = r;
                return r;
            }
        }

        /// <summary>The houses of a tile within the B0 band (plus the fade) of a centre, with every B1 block each part of
        /// it lies in (blocks clamp at the tile's edge, as MeshParts.BlockOf does).</summary>
        private static void ForEachPart(TileId id, B0HouseIndex idx, BandConfig bands, double cx, double cz, Action<int, int, double, double> visit)
        {
            double reach = bands.B0OuterM + BandConfig.FadeM * 0.5;
            int per = idx.PerSide(true);
            double side = idx.CellSide(true), spill = idx.Spill(true);
            int blocks = MeshParts.BlocksPerSide(id.Size, BandConfig.B1BlockM);
            int x0 = Math.Max(0, (int)Math.Floor((cx - reach - spill) / side)), x1 = Math.Min(per - 1, (int)Math.Floor((cx + reach + spill) / side));
            int z0 = Math.Max(0, (int)Math.Floor((cz - reach - spill) / side)), z1 = Math.Min(per - 1, (int)Math.Floor((cz + reach + spill) / side));
            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                    foreach (int i in idx.Houses(true, ix, iz))
                    {
                        float bx0, bz0, bx1, bz1;
                        idx.HouseBounds(i, out bx0, out bz0, out bx1, out bz1);
                        if (BandConfig.NearestDistance(bx0, bz0, bx1, bz1, cx, cz) > reach) continue;
                        int kx0 = Clamp((int)Math.Floor(bx0 / BandConfig.B1BlockM), blocks), kx1 = Clamp((int)Math.Floor(bx1 / BandConfig.B1BlockM), blocks);
                        int kz0 = Clamp((int)Math.Floor(bz0 / BandConfig.B1BlockM), blocks), kz1 = Clamp((int)Math.Floor(bz1 / BandConfig.B1BlockM), blocks);
                        for (int kz = kz0; kz <= kz1; kz++)
                            for (int kx = kx0; kx <= kx1; kx++)
                            {
                                // The part of the house in block (kx, kz); edge blocks reach beyond the tile.
                                double px0 = kx == 0 ? bx0 : Math.Max(bx0, kx * BandConfig.B1BlockM), px1 = kx == blocks - 1 ? bx1 : Math.Min(bx1, (kx + 1) * BandConfig.B1BlockM);
                                double pz0 = kz == 0 ? bz0 : Math.Max(bz0, kz * BandConfig.B1BlockM), pz1 = kz == blocks - 1 ? bz1 : Math.Min(bz1, (kz + 1) * BandConfig.B1BlockM);
                                double dmin = BandConfig.NearestDistance(px0, pz0, px1, pz1, cx, cz);
                                double fx = Math.Max(Math.Abs(px0 - cx), Math.Abs(px1 - cx)), fz = Math.Max(Math.Abs(pz0 - cz), Math.Abs(pz1 - cz));
                                visit(i, kz * blocks + kx, dmin, Math.Sqrt(fx * fx + fz * fz));
                            }
                    }
        }

        /// <summary>How far the band centre must move for a cell to hide and show again (out past the exit pad, back in
        /// within the lead): a re-show after less is a toggle. Heading back towards the cell at <paramref name="speed"/>
        /// m/s, the need lead (BandConfig.B0NeedLeadS of the speed) brings it in that much earlier on purpose, so its
        /// build is up before its houses reach the band: that share is anticipation, not a toggle (the anchor's dead zone
        /// is the least it can move).</summary>
        internal static double CellHysteresisM(BandConfig bands, double speed = 0)
        {
            double lead = speed * bands.B0NeedLeadS;
            if (lead < 0.5) lead = 0; // B0CellSet.Lead: no lead while standing or creeping
            return Math.Max(BandAnchor.DeadzoneM, B0CellSet.ExitPadM + bands.B0BuildPadM - bands.B0LeadM - Math.Min(lead, B0CellSet.MaxLeadM));
        }

        /// <summary>A key for something numbered <paramref name="n"/> (below 2^20) on a level-10 tile.</summary>
        private static long Key(TileId id, int n)
        {
            return (long)id.Tx << 32 | (long)id.Ty << 20 | (long)n;
        }

        private static int Clamp(int k, int n)
        {
            return k < 0 ? 0 : k >= n ? n - 1 : k;
        }

        // ------------------------------------------------------------------------------------------------------------
        // The cell set on the ride

        /// <summary>B0CellSet with a recording sink and ground, inline slices and the worker cost model on a frame clock.</summary>
        internal sealed class StableBench
        {
            public readonly int Tier;
            public readonly BandConfig Bands;
            public readonly MeshingSettings Settings;
            public readonly MeshingBuildingB0CellTests.Sink Sink = new MeshingBuildingB0CellTests.Sink();
            public readonly MeshingBuildingB0CellTests.Ground Ground;
            public readonly B0CellSet Set;
            public readonly Dictionary<TileId, B0Tile> Tiles = new Dictionary<TileId, B0Tile>();
            public double NowMs, Intra, Last;
            private readonly Dictionary<B0CellSet.Version, int> _charged = new Dictionary<B0CellSet.Version, int>();

            /// <summary>Main-thread milliseconds (Stopwatch) of the last <see cref="Frame"/>'s upload and update, the build
            /// slice excluded when <see cref="Deferred"/> runs it after them (as the game's worker does).</summary>
            public double MainMs;

            private readonly DeferredRunner _deferred;

            public StableBench(int tier, bool deferred = false)
            {
                Tier = tier;
                Bands = BandConfig.ForTier(tier);
                Settings = MeshingSettings.ForTier(tier);
                Ground = new MeshingBuildingB0CellTests.Ground(Settings.Roads);
                _deferred = deferred ? new DeferredRunner() : null;
                Set = new B0CellSet(Bands, Settings.DetailBuildings, Sink, Ground, _deferred ?? (ITileJobRunner)new InlineJobRunner(), Clock);
            }

            /// <summary>True when the slices run after the frame's main-thread work instead of inside it.</summary>
            public bool Deferred
            {
                get { return _deferred != null; }
            }

            /// <summary>Frame time plus the modelled cost of the houses the running slice has processed.</summary>
            private double Clock()
            {
                B0CellSet.Version v = Set.Job;
                if (v != null && v.From != null)
                {
                    int next;
                    _charged.TryGetValue(v, out next);
                    for (; next < v.Next; next++) Intra += v.From[next] >= 0 ? CopyMs : BuildMs[v.Levels[next]];
                    _charged[v] = next;
                }
                double t = Math.Max(Last, NowMs + Intra);
                Last = t;
                return t;
            }

            public void AddTilesAround(double x, double z)
            {
                TileId c = TileId.At(10, x, z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                        if (Tiles.ContainsKey(id)) continue;
                        TileData t = StreamingSampleRegion.Tile(id);
                        if (t == null || !t.HasDetail) continue;
                        Tiles[id] = Set.Add(id, t, TileHeightSampler.ForArea(t, id, 1), null);
                    }
            }

            /// <summary>The streamer's upload policy (WorldStreamer.Tick) with tiles streaming in every frame and taking all
            /// of the cap they are given (<see cref="RideCells"/> with <c>tilesStream</c>); null: B0 has the whole 1 MB cap.</summary>
            public B0UploadShare Share;

            /// <summary>With <see cref="Share"/>: false to ignore <see cref="B0CellSet.UrgentUploadWaiting"/> (the streamer
            /// before this package's playtest round: B0 went first only after three starved frames).</summary>
            public bool UrgentFirst = true;

            /// <summary>B0 bytes uploaded by the last <see cref="Frame"/>, and the most in one frame.</summary>
            public long FrameUploadBytes, MostUploadBytes;

            /// <summary>One frame as the streamer runs it: uploads (Tick), then the cells' update (UpdateView).</summary>
            public void Frame(double x, double z)
            {
                Intra = 0;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (Share == null) FrameUploadBytes = Set.Upload(1L << 20, true);
                else
                {
                    // WorldStreamer.Tick, the tiles taking every byte and millisecond they are left.
                    const long cap = 1L << 20;
                    bool waiting = Set.NextUploadBytes > 0;
                    long first = Share.First(cap, waiting, UrgentFirst && waiting && Set.UrgentUploadWaiting), b0 = 0;
                    if (first > 0) b0 = Set.Upload(first, true);
                    long tiles = cap - b0;
                    long after = Share.After(cap, b0 + tiles, waiting, first > 0, 0.0);
                    if (after > 0) b0 += Set.Upload(after, false);
                    Share.EndFrame(waiting, b0);
                    FrameUploadBytes = b0;
                }
                MostUploadBytes = Math.Max(MostUploadBytes, FrameUploadBytes);
                Set.Update(x, z);
                MainMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _deferred?.Drain();
                NowMs += FrameMs;
            }
        }

        /// <summary>Runs the one job the cell set started in a frame after the frame's main-thread work (the game's
        /// worker thread, without the thread).</summary>
        internal sealed class DeferredRunner : ITileJobRunner
        {
            private Action _work;

            public void Run(Action work)
            {
                Assert.IsNull(_work, "one B0 job at a time");
                _work = work;
            }

            public void Drain()
            {
                Action w = _work;
                _work = null;
                w?.Invoke();
            }
        }

        /// <summary>What the shown cells hold: houses per level, near-level houses beyond the near radius and its
        /// hysteresis, houses wholly beyond the band (clipped), triangles per level.</summary>
        private static string PeakNote(StableBench b, double x, double z)
        {
            var count = new int[3];
            var tris = new long[3];
            int farNear = 0, clipped = 0, cells = 0, padRing = 0;
            long stale = 0;
            long clippedTris = 0;
            foreach (B0Tile t in b.Tiles.Values)
            {
                if (t.Index == null) continue;
                double cx = x - t.Id.X0, cz = z - t.Id.Z0;
                foreach (B0CellSet.Cell cell in t.Cells.Values)
                {
                    if (!cell.Shown || cell.Live == null) continue;
                    cells++;
                    B0CellSet.Version v = cell.Live;
                    int[] houses = t.Index.Houses(b.Bands.B0FineCells, cell.X, cell.Z);
                    for (int k = 0; k < v.Positions.Length; k++)
                    {
                        int i = houses[v.Positions[k]];
                        int lv = v.Levels[k];
                        int tr = v.Ranges == null ? 0 : (v.Ranges[6 * k + 7] - v.Ranges[6 * k + 1]) / 3;
                        count[lv]++;
                        tris[lv] += tr;
                        if (lv == 0 && t.Index.CentroidDistance(i, cx, cz) > b.Bands.B0NearOuterM + BandConfig.B0LevelHysteresisM) farNear++;
                        if (!t.Index.Meets(i, b.Bands.B0OuterM, cx, cz, 0))
                        {
                            clipped++;
                            clippedTris += tr;
                            if (t.Index.Meets(i, b.Bands.B0OuterM, cx, cz, b.Bands.B0BuildPadM)) padRing++;
                            else stale += tr;
                        }
                    }
                }
            }
            return string.Format("{0} cells; near {1} ({2} k), lite {3} ({4} k), flat {5} ({6} k); {7} near beyond near+hyst; {8} beyond the band ({9} k; {10} in the pad ring, {11} k further)", cells,
                                 count[0], tris[0] / 1000, count[1], tris[1] / 1000, count[2], tris[2] / 1000, farNear, clipped, clippedTris / 1000, padRing, stale / 1000);
        }

        /// <summary>Rides the cell set along the frames on a tier, the band centred where <paramref name="centre"/> says
        /// (the streamer's choice), and counts what the bands draw.</summary>
        internal static Tally RideCells(int tier, List<RideFrame> frames, Func<RideFrame, double[]> centre, Action<int, StableBench, double[]> onFrame = null,
                                        bool deferred = false, bool tilesStream = false, bool urgentFirst = true)
        {
            var b = new StableBench(tier, deferred);
            var tally = new Tally();
            var drawable = new Drawable(b.Bands, b.Settings.DetailBuildings);
            double[] c0 = centre(frames[0]);
            b.AddTilesAround(c0[0], c0[1]);
            for (int f = 0; f < 3000; f++)
            {
                b.Frame(c0[0], c0[1]);
                if (b.Set.Settled && f > 2) break;
            }
            Assert.IsTrue(b.Set.Settled, TierName[tier] + ": B0 settles at the start");
            if (tilesStream)
            {
                // From the start of the ride, tiles stream in every frame (the settle above is the loading screen).
                b.Share = new B0UploadShare();
                b.UrgentFirst = urgentFirst;
                b.MostUploadBytes = 0;
            }

            var blockMode = new Dictionary<long, bool>();
            var shownState = new Dictionary<long, bool>();
            var hiddenAt = new Dictionary<long, double>();
            var hiddenWhere = new Dictionary<long, (double x, double z)>();
            var hiddenWhy = new Dictionary<long, string>();
            var levelOf = new Dictionary<long, int>();
            var lastChange = new Dictionary<long, (int from, double d)>();
            var drawn = new Dictionary<long, int>();
            var levels = new Dictionary<long, int>();
            var decided = new Dictionary<long, double>();
            int stop = -1, buildsAtSettle = -1;
            double stopStart = 0;
            bool first = true;
            foreach (RideFrame fr in frames)
            {
                double[] c = centre(fr);
                b.AddTilesAround(c[0], c[1]);
                int builds = b.Set.Builds, flipsBefore = tally.BlockFlips, reshowsBefore = tally.Reshows;
                b.Frame(c[0], c[1]);
                tally.Frames++;
                if (!fr.Stopped) tally.PeakMovingTris = Math.Max(tally.PeakMovingTris, b.Set.ShownTris);
                {
                    double vx = fr.RiderX - fr.CamX, vz = fr.RiderZ - fr.CamZ, vl = Math.Sqrt(vx * vx + vz * vz);
                    tally.PeakInViewTris = Math.Max(tally.PeakInViewTris, b.Sink.InViewTris(fr.CamX, fr.CamZ, vx / vl, vz / vl));
                }
                if (b.Set.ShownTris > tally.PeakShownTris)
                {
                    tally.PeakShownTris = b.Set.ShownTris;
                    tally.PeakNote = PeakNote(b, c[0], c[1]) + " at t " + fr.T.ToString("0.0") + " s v " +
                                     Math.Sqrt(b.Set.VelocityX * b.Set.VelocityX + b.Set.VelocityZ * b.Set.VelocityZ).ToString("0.0");
                }

                // Rebuilds while stopped, once the set has caught up with the stop.
                if (fr.Stop != stop)
                {
                    stop = fr.Stop;
                    stopStart = fr.T;
                    buildsAtSettle = -1;
                }
                if (fr.Stopped)
                {
                    // Once the rider has stood a second (the speed estimate has decayed) and the set has caught up.
                    if (buildsAtSettle < 0 && b.Set.Settled && fr.T - stopStart >= 1.0) buildsAtSettle = b.Set.Builds;
                    else if (buildsAtSettle >= 0 && b.Set.Builds > builds) tally.StopBuilds += b.Set.Builds - builds;
                }

                // What B0 draws, and the cells' show and hide history.
                drawn.Clear();
                levels.Clear();
                decided.Clear();
                int mismatches = 0;
                foreach (B0Tile t in b.Tiles.Values)
                {
                    if (t.Index == null) continue;
                    int per = t.Index.PerSide(b.Bands.B0FineCells);
                    foreach (B0CellSet.Cell cell in t.Cells.Values)
                    {
                        long ck = Key(t.Id, cell.Z * 1024 + cell.X);
                        bool was;
                        shownState.TryGetValue(ck, out was);
                        if (cell.Shown != was)
                        {
                            // A cell shown again before the centre moved the cell hysteresis since it hid (out past the exit
                            // pad, back within the lead): a toggle. Riding away and turning back is not.
                            double speed = Math.Sqrt(b.Set.VelocityX * b.Set.VelocityX + b.Set.VelocityZ * b.Set.VelocityZ);
                            if (cell.Shown && hiddenAt.ContainsKey(ck) &&
                                Math.Sqrt(Math.Pow(c[0] - hiddenWhere[ck].x, 2) + Math.Pow(c[1] - hiddenWhere[ck].z, 2)) < CellHysteresisM(b.Bands, speed))
                            {
                                tally.Reshows++;
                                if (b.NowMs - hiddenAt[ck] < 2000) tally.QuickReshows++;
                                if (tally.FirstReshow == null)
                                {
                                    float x0, z0, x1, z1;
                                    t.Index.CellBounds(b.Bands.B0FineCells, cell.X, cell.Z, out x0, out z0, out x1, out z1);
                                    tally.FirstReshow = "cell " + cell.X + "," + cell.Z + " of " + t.Id + " hidden (" + hiddenWhy[ck] + ") " + ((b.NowMs - hiddenAt[ck]) / 1000).ToString("0.0") +
                                                        " s at t " + fr.T.ToString("0.0") + ", " +
                                                        BandConfig.NearestDistance(x0, z0, x1, z1, c[0] - t.Id.X0, c[1] - t.Id.Z0).ToString("0.0") + " m away";
                                }
                            }
                            if (!cell.Shown)
                            {
                                hiddenAt[ck] = b.NowMs;
                                hiddenWhere[ck] = (c[0], c[1]);
                                hiddenWhy[ck] = cell.WantedFrame != b.Set.Frame ? "unwanted" : cell.Live == null ? "no version" : cell.Failed ? "failed" : "blocks";
                            }
                            shownState[ck] = cell.Shown;
                        }
                        // Colliders: a shown cell registers exactly its version's set, a hidden one none.
                        ulong key = B0CellSet.CellKey(t.Id, per, cell.X, cell.Z);
                        int registered;
                        bool has = b.Ground.Sets.TryGetValue(key, out registered);
                        int want = cell.Shown && cell.Live != null && cell.Live.Colliders != null ? cell.Live.Colliders.Count : 0;
                        if (want > 0 ? !has || registered != want : has) mismatches++;
                        if (!cell.Shown || cell.Live == null) continue;
                        int[] houses = t.Index.Houses(b.Bands.B0FineCells, cell.X, cell.Z);
                        for (int k = 0; k < cell.Live.Positions.Length; k++)
                        {
                            int hi = houses[cell.Live.Positions[k]];
                            long hk = Key(t.Id, hi);
                            int n;
                            drawn.TryGetValue(hk, out n);
                            drawn[hk] = n + 1;
                            levels[hk] = cell.Live.Levels[k];
                            decided[hk] = t.Index.CentroidDistance(hi, cell.Live.CamX, cell.Live.CamZ);
                        }
                    }
                }
                if (mismatches > 0) tally.ColliderMismatches++;

                // Level reversals: a house switching level and back while its distance to the centre (where each switch was
                // decided: its version's build) changed less than the level hysteresis (passing a house, it changes on the
                // way in and back on the way out: planned).
                foreach (KeyValuePair<long, int> kv in levels)
                {
                    int old;
                    if (levelOf.TryGetValue(kv.Key, out old) && old != kv.Value)
                    {
                        double d = decided[kv.Key];
                        (int from, double d) prev;
                        if (lastChange.TryGetValue(kv.Key, out prev) && prev.from == kv.Value && Math.Abs(d - prev.d) < BandConfig.B0LevelHysteresisM)
                        {
                            tally.LevelReversals++;
                            if (tally.FirstReversal == null)
                                tally.FirstReversal = "house " + (kv.Key & 1048575) + " " + old + "->" + kv.Value + " back at " + d.ToString("0.0") + " m (was " + prev.d.ToString("0.0") +
                                                      " m) at t " + fr.T.ToString("0.0");
                        }
                        lastChange[kv.Key] = (old, d);
                    }
                    levelOf[kv.Key] = kv.Value;
                }

                // Every part of every house near the band against what the bands draw.
                bool gapFrame = false, dblFrame = false;
                int standing = 0;
                foreach (KeyValuePair<TileId, B0Tile> kv in b.Tiles)
                {
                    B0Tile t = kv.Value;
                    if (t.Index == null) continue;
                    int blocks = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                    for (int k = 0; k < blocks * blocks; k++)
                    {
                        bool full = !b.Set.BlockReady(t, k);
                        long bk = Key(t.Id, k);
                        bool before;
                        if (blockMode.TryGetValue(bk, out before) && before != full && !first)
                        {
                            tally.BlockFlips++;
                            if (tally.FirstFlip == null)
                                tally.FirstFlip = "block " + k + " of " + t.Id + " at " + fr.T.ToString("0.0") + " s" + (full ? " standing in: " + WhyStandIn(b, t, k, c[0], c[1]) : " handed over");
                        }
                        blockMode[bk] = full;
                    }
                    double cx = c[0] - t.Id.X0, cz = c[1] - t.Id.Z0;
                    ForEachPart(t.Id, t.Index, b.Bands, cx, cz, (i, block, dmin, dmax) =>
                    {
                        long hk = Key(t.Id, i);
                        int n;
                        drawn.TryGetValue(hk, out n);
                        var d = new Drawers();
                        for (int q = 0; q < n; q++) d.Add(0f, b.Bands.B0OuterM);
                        bool full = !b.Set.BlockReady(t, block);
                        if (full) standing++;
                        bool gap, dbl, ho;
                        Cover(dmin, dmax, d, full, b.Bands, out gap, out dbl, out ho);
                        if (gap && n == 0 && !drawable.Of(t.Id, t.Source, t.Sampler, i)) gap = false;
                        if (ho) tally.Handovers++;
                        if (gap)
                        {
                            gapFrame = true;
                            tally.GapParts++;
                            tally.GapHouses.Add(hk);
                            if (tally.FirstGap == null)
                                tally.FirstGap = "house " + i + " of " + t.Id + " at " + dmin.ToString("0.0") + "-" + dmax.ToString("0.0") + " m, t " + fr.T.ToString("0.0") + " s";
                        }
                        if (dbl)
                        {
                            dblFrame = true;
                            tally.DoubleParts++;
                            tally.DoubleHouses.Add(hk);
                            if (tally.FirstDouble == null) tally.FirstDouble = "house " + i + " of " + t.Id + " at t " + fr.T.ToString("0.0") + " s";
                        }
                    });
                }
                if (gapFrame) tally.GapFrames++;
                if (dblFrame) tally.DoubleFrames++;
                tally.Mark(gapFrame, dblFrame, flipsBefore, reshowsBefore);
                tally.BlocksStandingIn = Math.Max(tally.BlocksStandingIn, standing);
                first = false;
                onFrame?.Invoke(tally.Frames - 1, b, c);
            }
            tally.Work = string.Format("{0} builds by reason {1}, {2} houses built, {3} copied, {4} slices, {5:0} ms of work; at most {6:0.00} MB uploaded in a frame",
                                       b.Set.Builds, string.Join("/", b.Set.BuildsByReason), b.Set.HousesBuilt, b.Set.HousesReused, b.Set.Slices, b.Set.WorkMs,
                                       b.MostUploadBytes / 1048576.0);
            tally.MostUploadBytes = b.MostUploadBytes;
            b.Set.Dispose();
            return tally;
        }

        /// <summary>Why block <paramref name="k"/> of a tile stands in: each house the band draws in it that its cell's shown
        /// version lacks, with the state of that cell (diagnostics for a failing ride).</summary>
        private static string WhyStandIn(StableBench b, B0Tile t, int k, double x, double z)
        {
            var sb = new System.Text.StringBuilder();
            double cx = x - t.Id.X0, cz = z - t.Id.Z0;
            int per = t.Index.PerSide(b.Bands.B0FineCells);
            double v = Math.Sqrt(b.Set.VelocityX * b.Set.VelocityX + b.Set.VelocityZ * b.Set.VelocityZ);
            sb.Append("v ").Append(v.ToString("0.0")).Append(" m/s, ready ").Append(b.Set.Busy ? "busy" : "idle").Append("; ");
            int shown = 0;
            for (int iz = 0; iz < per; iz++)
                for (int ix = 0; ix < per; ix++)
                {
                    int[] houses = t.Index.Houses(b.Bands.B0FineCells, ix, iz);
                    B0CellSet.Cell cell;
                    t.Cells.TryGetValue(iz * 4096 + ix, out cell);
                    for (int p = 0; p < houses.Length; p++)
                    {
                        int i = houses[p];
                        if (!t.Index.Meets(i, b.Bands.B0OuterM, cx, cz, 0)) continue;
                        int kx0, kz0, kx1, kz1;
                        t.Index.HouseBlocks(i, t.BlocksPerSide, out kx0, out kz0, out kx1, out kz1);
                        if (k % t.BlocksPerSide < kx0 || k % t.BlocksPerSide > kx1 || k / t.BlocksPerSide < kz0 || k / t.BlocksPerSide > kz1) continue;
                        bool has = cell != null && cell.Live != null && cell.Live.Has(p);
                        if (has || shown >= 3) continue;
                        shown++;
                        sb.Append("house ").Append(i).Append(" at ").Append(t.Index.Distance(i, cx, cz).ToString("0.0")).Append(" m in cell ").Append(ix).Append(',').Append(iz);
                        if (cell == null) sb.Append(" (no cell)");
                        else
                        {
                            sb.Append(cell.WantedFrame == b.Set.Frame ? " wanted" : " unwanted").Append(cell.Failed ? " failed" : "");
                            sb.Append(cell.Live == null ? ", no live" : ", live of " + cell.Live.Positions.Length + " built " +
                                                                       BandConfig.NearestDistance(cx, cz, cx, cz, cell.Live.CamX, cell.Live.CamZ).ToString("0.0") + " m back");
                            if (cell.Pending != null)
                                sb.Append(", pending ").Append(cell.Pending.Built ? "built" : "building " + cell.Pending.Next + "/" + cell.Pending.Positions.Length)
                                  .Append(" up ").Append(cell.Pending.Uploaded).Append('/').Append(cell.Pending.Chunks.Count).Append(cell.Pending.Has(p) ? " with it" : " without it");
                        }
                        sb.Append("; ");
                    }
                }
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------------------
        // The old ring cells (main at 5a7668a), engine-free, with the same cost model

        /// <summary>
        /// DetailCells as the owner played it: a near ring of 32 m cells out to BuildingBandTable.NearOuterM and a lite
        /// ring of 64 m cells out to the B0 radius, each cell wanted while its square grown by 24 m touches its ring and
        /// built whole (every house of the cell at the ring's level) on one worker, one build started per 100 ms at most;
        /// a cell shows when it is up, wanted and its B1 block is ready, and a B1 block stands in (its cells hidden) while
        /// any cell of either ring the camera wants in it is not up; an LRU of 24 / 48 / 96 cells.
        /// </summary>
        internal sealed class OldRings
        {
            private sealed class Cell
            {
                public int Layer, X, Z;
                public bool Uploaded, Shown;
                public double LastUsed, DoneAt;
            }

            private sealed class Tile
            {
                public TileId Id;
                public TileData Source;
                public TileHeightSampler Sampler;
                public B0HouseIndex Index;
                public readonly Dictionary<int, Cell> Cells = new Dictionary<int, Cell>();
                public readonly Dictionary<int, int> NearCell = new Dictionary<int, int>(), LiteCell = new Dictionary<int, int>();
            }

            private const float MarginM = 24f;
            private readonly int _tier;
            private readonly float _near, _outer;
            private readonly int _cache;
            public readonly BandConfig Bands;
            private readonly List<Tile> _tiles = new List<Tile>();
            private readonly List<Cell> _all = new List<Cell>();
            private readonly Dictionary<Cell, Tile> _owner = new Dictionary<Cell, Tile>();
            private Cell _job;
            private double _lastDispatch = -1e9;

            public OldRings(int tier)
            {
                _tier = tier;
                Bands = BandConfig.ForTier(tier);
                _near = BuildingBandTable.NearOuterM(tier);
                _outer = BuildingBandTable.OuterM(tier, 0);
                _cache = tier == 0 ? 24 : tier == 1 ? 48 : 96;
            }

            public void AddTilesAround(double x, double z)
            {
                TileId c = TileId.At(10, x, z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                        if (_tiles.Exists(t => t.Id.Equals(id))) continue;
                        TileData src = StreamingSampleRegion.Tile(id);
                        if (src == null || !src.HasDetail) continue;
                        var tile = new Tile { Id = id, Source = src, Sampler = TileHeightSampler.ForArea(src, id, 1), Index = B0HouseIndex.Prepare(src) };
                        foreach (bool fine in new[] { true, false })
                        {
                            int per = tile.Index.PerSide(fine);
                            for (int iz = 0; iz < per; iz++)
                                for (int ix = 0; ix < per; ix++)
                                    foreach (int i in tile.Index.Houses(fine, ix, iz)) (fine ? tile.NearCell : tile.LiteCell)[i] = iz * 4096 + ix;
                        }
                        _tiles.Add(tile);
                    }
            }

            private static int Key(int layer, int cx, int cz)
            {
                return layer << 24 | cz * 4096 + cx;
            }

            private void Range(int layer, out float inner, out float outer)
            {
                inner = layer == 0 ? 0f : _near;
                outer = layer == 0 ? _near : _outer;
            }

            private bool Wanted(int layer, double x0, double z0, double x1, double z1, double cx, double cz)
            {
                float inner, outer;
                Range(layer, out inner, out outer);
                return BandConfig.Touches(x0 - MarginM, z0 - MarginM, x1 + MarginM, z1 + MarginM, cx, cz, inner, outer);
            }

            private double CellSide(Tile t, int layer)
            {
                return t.Index.CellSide(layer == 0);
            }

            public void Update(double camX, double camZ, double now)
            {
                if (_job != null && now >= _job.DoneAt)
                {
                    _job.Uploaded = true;
                    _job.LastUsed = now;
                    _job = null;
                }
                Tile bestTile = null;
                int bestLayer = 0, bestX = 0, bestZ = 0;
                double bestScore = double.MaxValue;
                float reach = _outer + BandConfig.FadeM + MarginM;
                foreach (Tile t in _tiles)
                {
                    double cx = camX - t.Id.X0, cz = camZ - t.Id.Z0;
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int per = t.Index.PerSide(layer == 0);
                        double cell = CellSide(t, layer);
                        int x0 = Math.Max(0, (int)Math.Floor((cx - reach) / cell)), x1 = Math.Min(per - 1, (int)Math.Floor((cx + reach) / cell));
                        int z0 = Math.Max(0, (int)Math.Floor((cz - reach) / cell)), z1 = Math.Min(per - 1, (int)Math.Floor((cz + reach) / cell));
                        for (int iz = z0; iz <= z1; iz++)
                            for (int ix = x0; ix <= x1; ix++)
                            {
                                if (!Wanted(layer, ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz)) continue;
                                Cell c;
                                if (t.Cells.TryGetValue(Key(layer, ix, iz), out c))
                                {
                                    c.LastUsed = now;
                                    continue;
                                }
                                double d = BandConfig.NearestDistance(ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz);
                                double score = d + (layer == 0 ? 0 : 8);
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestTile = t;
                                    bestLayer = layer;
                                    bestX = ix;
                                    bestZ = iz;
                                }
                            }
                    }
                }
                foreach (Cell c in _all)
                {
                    if (!c.Uploaded) continue;
                    Tile t = _owner[c];
                    double cx = camX - t.Id.X0, cz = camZ - t.Id.Z0, side = CellSide(t, c.Layer);
                    c.Shown = Wanted(c.Layer, c.X * side, c.Z * side, (c.X + 1) * side, (c.Z + 1) * side, cx, cz) &&
                              BlockReady(t, BlockOfCell(t, c), cx, cz);
                }
                if (_job == null && bestTile != null && now - _lastDispatch >= 100.0)
                {
                    var c = new Cell { Layer = bestLayer, X = bestX, Z = bestZ, LastUsed = now };
                    double cost = 0;
                    int level = bestLayer == 0 ? BandConfig.LevelNear : _tier == 0 ? BandConfig.LevelFlat : BandConfig.LevelLite;
                    foreach (int unused in bestTile.Index.Houses(bestLayer == 0, bestX, bestZ)) cost += BuildMs[level];
                    c.DoneAt = now + cost;
                    bestTile.Cells.Add(Key(bestLayer, bestX, bestZ), c);
                    _all.Add(c);
                    _owner[c] = bestTile;
                    _job = c;
                    _lastDispatch = now;
                }
                int over = _all.Count - _cache;
                while (over > 0)
                {
                    Cell oldest = null;
                    foreach (Cell c in _all)
                    {
                        if (c == _job || c.LastUsed >= now) continue;
                        if (oldest == null || c.LastUsed < oldest.LastUsed) oldest = c;
                    }
                    if (oldest == null) break;
                    _owner[oldest].Cells.Remove(Key(oldest.Layer, oldest.X, oldest.Z));
                    _owner.Remove(oldest);
                    _all.Remove(oldest);
                    over--;
                }
            }

            private static int BlockOfCell(Tile t, Cell c)
            {
                int per = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                double side = t.Index.CellSide(c.Layer == 0);
                return MeshParts.BlockOf((c.X + 0.5) * side, (c.Z + 0.5) * side, BandConfig.B1BlockM, per);
            }

            private bool BlockReady(Tile t, int block, double cx, double cz)
            {
                int blocks = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                double b0x = (block % blocks) * BandConfig.B1BlockM, b0z = (block / blocks) * BandConfig.B1BlockM;
                float reach = _outer + BandConfig.FadeM + MarginM;
                if (BandConfig.NearestDistance(b0x, b0z, b0x + BandConfig.B1BlockM, b0z + BandConfig.B1BlockM, cx, cz) > reach) return true;
                for (int layer = 0; layer < 2; layer++)
                {
                    int per = t.Index.PerSide(layer == 0);
                    double cell = CellSide(t, layer);
                    int ix0 = Math.Max(0, (int)Math.Floor(b0x / cell + 1e-6)), iz0 = Math.Max(0, (int)Math.Floor(b0z / cell + 1e-6));
                    int ix1 = Math.Min(per - 1, (int)Math.Ceiling((b0x + BandConfig.B1BlockM) / cell - 1e-6) - 1);
                    int iz1 = Math.Min(per - 1, (int)Math.Ceiling((b0z + BandConfig.B1BlockM) / cell - 1e-6) - 1);
                    for (int iz = iz0; iz <= iz1; iz++)
                        for (int ix = ix0; ix <= ix1; ix++)
                        {
                            if (!Wanted(layer, ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz)) continue;
                            Cell c;
                            if (!t.Cells.TryGetValue(Key(layer, ix, iz), out c) || !c.Uploaded) return false;
                        }
                }
                return true;
            }

            /// <summary>Every house a shown cell draws, with its ring's level and range, and whether each B1 block of a tile
            /// stands in, for a camera at (camX, camZ).</summary>
            public void Scene(SceneDump scene, double camX, double camZ, BuildingOptions shared)
            {
                BuildingOptions near = BuildingBandTable.NearOptions(_tier, shared), lite = BuildingBandTable.LiteOptions(_tier, shared);
                foreach (Tile t in _tiles)
                {
                    double cx = camX - t.Id.X0, cz = camZ - t.Id.Z0;
                    foreach (Cell c in t.Cells.Values)
                    {
                        if (!c.Shown) continue;
                        float inner, outer;
                        Range(c.Layer, out inner, out outer);
                        foreach (int i in t.Index.Houses(c.Layer == 0, c.X, c.Z))
                            scene.AddHouse(t.Id, t.Source, t.Sampler, i, c.Layer == 0 ? near : lite, inner, outer);
                    }
                    scene.AddB1(t.Id, t.Source, t.Sampler, k => !BlockReady(t, k, cx, cz));
                }
            }

            /// <summary>Every B1 block of every tile: whether it stands in, and its distance from (camX, camZ).</summary>
            public void ForEachBlock(double camX, double camZ, Action<TileId, int, bool, double> visit)
            {
                foreach (Tile t in _tiles)
                {
                    double cx = camX - t.Id.X0, cz = camZ - t.Id.Z0;
                    int blocks = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                    for (int k = 0; k < blocks * blocks; k++)
                    {
                        double x0 = (k % blocks) * BandConfig.B1BlockM, z0 = (k / blocks) * BandConfig.B1BlockM;
                        visit(t.Id, k, !BlockReady(t, k, cx, cz), BandConfig.NearestDistance(x0, z0, x0 + BandConfig.B1BlockM, z0 + BandConfig.B1BlockM, cx, cz));
                    }
                }
            }

            public bool Settled(double camX, double camZ)
            {
                if (_job != null) return false;
                foreach (Tile t in _tiles)
                {
                    int blocks = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                    for (int k = 0; k < blocks * blocks; k++)
                        if (!BlockReady(t, k, camX - t.Id.X0, camZ - t.Id.Z0)) return false;
                }
                return true;
            }

            /// <summary>Counts what the old rings draw on the ride (camera-centred, as the old streamer did).</summary>
            public static Tally Ride(int tier, List<RideFrame> frames, Action<int, OldRings> onFrame = null)
            {
                var o = new OldRings(tier);
                var tally = new Tally();
                MeshingSettings settings = MeshingSettings.ForTier(tier);
                var drawable = new Drawable(o.Bands, settings.DetailBuildings);
                double now = 0;
                RideFrame f0 = frames[0];
                o.AddTilesAround(f0.CamX, f0.CamZ);
                for (int f = 0; f < 3000 && !o.Settled(f0.CamX, f0.CamZ); f++)
                {
                    o.Update(f0.CamX, f0.CamZ, now);
                    now += FrameMs;
                }
                var blockMode = new Dictionary<long, bool>();
                var shownState = new Dictionary<Cell, bool>();
                var hiddenAt = new Dictionary<Cell, double>();
                var hiddenWhere = new Dictionary<Cell, (double x, double z)>();
                bool first = true;
                foreach (RideFrame fr in frames)
                {
                    int flipsBefore = tally.BlockFlips, reshowsBefore = tally.Reshows;
                    o.AddTilesAround(fr.CamX, fr.CamZ);
                    o.Update(fr.CamX, fr.CamZ, now);
                    now += FrameMs;
                    tally.Frames++;
                    foreach (Cell c in o._all)
                    {
                        bool was;
                        shownState.TryGetValue(c, out was);
                        if (c.Shown == was) continue;
                        if (c.Shown && hiddenAt.ContainsKey(c) &&
                            Math.Sqrt(Math.Pow(fr.CamX - hiddenWhere[c].x, 2) + Math.Pow(fr.CamZ - hiddenWhere[c].z, 2)) < CellHysteresisM(o.Bands))
                        {
                            tally.Reshows++;
                            if (now - hiddenAt[c] < 2000) tally.QuickReshows++;
                        }
                        if (!c.Shown)
                        {
                            hiddenAt[c] = now;
                            hiddenWhere[c] = (fr.CamX, fr.CamZ);
                        }
                        shownState[c] = c.Shown;
                    }
                    bool gapFrame = false, dblFrame = false;
                    foreach (Tile t in o._tiles)
                    {
                        double cx = fr.CamX - t.Id.X0, cz = fr.CamZ - t.Id.Z0;
                        int blocks = MeshParts.BlocksPerSide(t.Id.Size, BandConfig.B1BlockM);
                        var full = new bool[blocks * blocks];
                        for (int k = 0; k < full.Length; k++)
                        {
                            full[k] = !o.BlockReady(t, k, cx, cz);
                            long bk = MeshingBuildingB0StabilityTests.Key(t.Id, k);
                            bool before;
                            if (blockMode.TryGetValue(bk, out before) && before != full[k] && !first)
                            {
                                tally.BlockFlips++;
                                if (tally.FirstFlip == null) tally.FirstFlip = "block " + k + " of " + t.Id + " at " + fr.T.ToString("0.0") + " s";
                            }
                            blockMode[bk] = full[k];
                        }
                        ForEachPart(t.Id, t.Index, o.Bands, cx, cz, (i, block, dmin, dmax) =>
                        {
                            var d = new Drawers();
                            Cell c;
                            int key;
                            if (t.NearCell.TryGetValue(i, out key) && t.Cells.TryGetValue(key, out c) && c.Shown) d.Add(0f, o._near);
                            if (t.LiteCell.TryGetValue(i, out key) && t.Cells.TryGetValue(Key(1, key % 4096, key / 4096), out c) && c.Shown) d.Add(o._near, o._outer);
                            bool gap, dbl, ho;
                            Cover(dmin, dmax, d, full[block], o.Bands, out gap, out dbl, out ho);
                            if (gap && d.Count == 0 && !drawable.Of(t.Id, t.Source, t.Sampler, i)) gap = false;
                            long hk = MeshingBuildingB0StabilityTests.Key(t.Id, i);
                            if (ho) tally.Handovers++;
                            if (gap)
                            {
                                gapFrame = true;
                                tally.GapParts++;
                                tally.GapHouses.Add(hk);
                                if (tally.FirstGap == null) tally.FirstGap = "house " + i + " of " + t.Id + " at t " + fr.T.ToString("0.0") + " s";
                            }
                            if (dbl)
                            {
                                dblFrame = true;
                                tally.DoubleParts++;
                                tally.DoubleHouses.Add(hk);
                            }
                        });
                    }
                    if (gapFrame) tally.GapFrames++;
                    if (dblFrame) tally.DoubleFrames++;
                    tally.Mark(gapFrame, dblFrame, flipsBefore, reshowsBefore);
                    first = false;
                    onFrame?.Invoke(tally.Frames - 1, o);
                }
                return tally;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Street dumps (GHUMANTE_PREVIEW_DIR)

        /// <summary>
        /// What the bands draw at one frame, as one scene in a home tile's frame: B0 houses cut to their band range (the
        /// dithered fade cut at its middle, as the shadow pass does), each B1 block's triangles from the centre (standing in)
        /// or from the B0 radius, and the terrain and roads, within <see cref="RadiusM"/> of the band centre.
        /// </summary>
        internal sealed class SceneDump
        {
            public const float RadiusM = 170f;
            private static readonly Dictionary<TileId, MeshData> B1Layers = new Dictionary<TileId, MeshData>();
            private readonly TileId _home;
            private readonly double _cx, _cz;
            private readonly BandConfig _bands;
            private readonly BuildingOptions _b1;
            private readonly MeshData _one = new MeshData();
            public readonly MeshData B0 = new MeshData(), B1 = new MeshData(), StandIn = new MeshData();

            public SceneDump(TileId home, double centreX, double centreZ, BandConfig bands, MeshingSettings settings)
            {
                _home = home;
                _cx = centreX;
                _cz = centreZ;
                _bands = bands;
                _b1 = settings.Buildings;
            }

            public void AddHouse(TileId id, TileData t, IHeightSampler h, int i, BuildingOptions o, float inner, float outer)
            {
                _one.Clear();
                if (!BuildingDetailMesher.One(t, i, h, o, _one, null)) return;
                Keep(_one, id, B0, (d, x, z) => d >= inner && d <= outer);
            }

            public void AddB1(TileId id, TileData t, IHeightSampler h, Func<int, bool> standsIn)
            {
                MeshData layer;
                lock (B1Layers)
                {
                    if (!B1Layers.TryGetValue(id, out layer))
                    {
                        layer = new MeshData();
                        BuildingMesher.Build(t, h, _b1, layer);
                        B1Layers[id] = layer;
                    }
                }
                int per = MeshParts.BlocksPerSide(id.Size, BandConfig.B1BlockM);
                Keep(layer, id, B1, (d, x, z) => !standsIn(MeshParts.BlockOf(x, z, BandConfig.B1BlockM, per)) && d >= _bands.B0OuterM && d <= _bands.B1OuterM);
                Keep(layer, id, StandIn, (d, x, z) => standsIn(MeshParts.BlockOf(x, z, BandConfig.B1BlockM, per)) && d <= _bands.B1OuterM);
            }

            /// <summary>Appends the triangles of <paramref name="m"/> (tile-local) whose centroid passes the test (distance
            /// from the centre, tile-local x and z) and lies within <see cref="RadiusM"/>, moved to the home tile's frame.</summary>
            private void Keep(MeshData m, TileId id, MeshData into, Func<double, double, double, bool> keep)
            {
                float dx = (float)(id.X0 - _home.X0), dz = (float)(id.Z0 - _home.Z0);
                double lcx = _cx - id.X0, lcz = _cz - id.Z0;
                for (int q = 0; q + 2 < m.IndexCount; q += 3)
                {
                    double px = 0, pz = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        px += m.Positions[3 * m.Indices[q + k]] / 3.0;
                        pz += m.Positions[3 * m.Indices[q + k] + 2] / 3.0;
                    }
                    double d = Math.Sqrt((px - lcx) * (px - lcx) + (pz - lcz) * (pz - lcz));
                    if (d > RadiusM || !keep(d, px, pz)) continue;
                    int b = into.VertexCount;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = m.Indices[q + k];
                        uint rgba = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                        float x = m.Positions[3 * v] + dx, y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2] + dz;
                        if (m.HasUv0) into.AddVertex(x, y, z, m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2], rgba, m.Uv0[2 * v], m.Uv0[2 * v + 1]);
                        else into.AddVertex(x, y, z, m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2], rgba);
                    }
                    into.AddTriangle(b, b + 1, b + 2);
                }
            }

            /// <summary>Writes b0, b1 and standin OBJs under <paramref name="dir"/>.</summary>
            public void Write(string dir)
            {
                ObjDump.Write(B0, dir + "/b0.obj");
                ObjDump.Write(B1, dir + "/b1.obj");
                ObjDump.Write(StandIn, dir + "/standin.obj");
            }
        }

        /// <summary>
        /// Dumps the street the rider sees when the old rings flip a B1 block next to the road (the frame before and the
        /// frame of the flip) and the same two frames with the B0 cells, High tier (the owner's), for
        /// tools/mesh-preview; prints the chase camera's eye and look in the home tile's frame (Unity axes).
        /// </summary>
        [Test]
        public void DumpFlickerFrames()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the street");
            const int tier = 2;
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            MeshingSettings settings = MeshingSettings.ForTier(tier);
            BandConfig bands = BandConfig.ForTier(tier);
            // The old rings: the first block (after 5 s) next to the rider that flips to standing in.
            int flip = -1;
            var was = new Dictionary<long, bool>();
            OldRings.Ride(tier, frames, (f, o) =>
            {
                if (flip >= 0) return;
                RideFrame fr = frames[f];
                o.ForEachBlock(fr.CamX, fr.CamZ, (id, k, full, near) =>
                {
                    long key = Key(id, k);
                    bool before;
                    if (flip < 0 && fr.T > 5 && near < 25 && was.TryGetValue(key, out before) && !before && full) flip = f;
                    was[key] = full;
                });
            });
            Assert.GreaterOrEqual(flip, 1, "the old rings flip a block next to the rider");
            var capture = new HashSet<int> { flip - 1, flip };
            OldRings.Ride(tier, frames, (f, o) =>
            {
                if (!capture.Contains(f)) return;
                RideFrame fr = frames[f];
                var scene = new SceneDump(TileId.At(10, fr.CamX, fr.CamZ), fr.CamX, fr.CamZ, bands, settings);
                o.Scene(scene, fr.CamX, fr.CamZ, settings.DetailBuildings);
                scene.Write("b0flicker/old_" + (f - flip + 1));
                Context(scene, fr, "old_" + (f - flip + 1));
            });
            var anchor = new BandAnchor();
            RideCells(tier, frames, fr =>
            {
                anchor.Follow(fr.CamX, fr.CamZ, fr.RiderX, fr.RiderZ, true);
                return new[] { anchor.X, anchor.Z };
            }, (f, b, c) =>
            {
                if (!capture.Contains(f)) return;
                RideFrame fr = frames[f];
                var scene = new SceneDump(TileId.At(10, fr.CamX, fr.CamZ), c[0], c[1], b.Bands, b.Settings);
                foreach (B0Tile t in b.Tiles.Values)
                {
                    if (t.Index == null) continue;
                    foreach (B0CellSet.Cell cell in t.Cells.Values)
                    {
                        if (!cell.Shown || cell.Live == null) continue;
                        int[] houses = t.Index.Houses(b.Bands.B0FineCells, cell.X, cell.Z);
                        for (int k = 0; k < cell.Live.Positions.Length; k++)
                            scene.AddHouse(t.Id, t.Source, t.Sampler, houses[cell.Live.Positions[k]], b.Bands.B0Options(cell.Live.Levels[k], b.Settings.DetailBuildings), 0f,
                                           b.Bands.B0OuterM);
                    }
                    B0Tile tt = t;
                    scene.AddB1(t.Id, t.Source, t.Sampler, k => !b.Set.BlockReady(tt, k));
                }
                scene.Write("b0flicker/new_" + (f - flip + 1));
                Context(scene, fr, "new_" + (f - flip + 1));
            });
        }

        /// <summary>
        /// The ride's timeline for the preview chart (set <c>GHUMANTE_PREVIEW_DIR</c>): per frame of the Thamel to Asan ride
        /// and per tier, what the old ring cells and the B0 cells showed (gap, double, block flip, re-show), written to
        /// <c>b0timeline/thamel_asan.csv</c> with whether the rider stands (the chart in the perf-cells previews).
        /// </summary>
        [Test]
        public void DumpRideTimelines()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the ride timeline");
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            var cols = new List<List<byte>>();
            for (int tier = 0; tier <= 2; tier++)
            {
                cols.Add(OldRings.Ride(tier, frames).Timeline);
                var anchor = new BandAnchor();
                cols.Add(RideCells(tier, frames, f =>
                {
                    anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                    return new[] { anchor.X, anchor.Z };
                }).Timeline);
            }
            var sb = new System.Text.StringBuilder("frame,t,stopped,low_old,low_new,mid_old,mid_new,high_old,high_new\n");
            for (int f = 0; f < frames.Count; f++)
            {
                sb.Append(f).Append(',').Append(frames[f].T.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(frames[f].Stopped ? 1 : 0);
                foreach (List<byte> c in cols) sb.Append(',').Append(f < c.Count ? c[f] : 0);
                sb.Append('\n');
            }
            string path = Path.Combine(ObjDump.Dir, "b0timeline", "thamel_asan.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            TestContext.WriteLine("timeline: " + path);
        }

        /// <summary>Terrain and roads around the frame, and the chase camera's eye and look (home tile frame, Unity axes).</summary>
        private static void Context(SceneDump scene, RideFrame fr, string name)
        {
            TileId home = TileId.At(10, fr.CamX, fr.CamZ);
            TileData src = StreamingSampleRegion.Tile(home);
            TileHeightSampler sampler = TileHeightSampler.ForArea(src, home, 1);
            float ex = (float)(fr.CamX - home.X0), ez = (float)(fr.CamZ - home.Z0), gy, ry;
            sampler.TryHeightClamped(fr.CamX, fr.CamZ, out gy);
            sampler.TryHeightClamped(fr.RiderX, fr.RiderZ, out ry);
            double hx = fr.RiderX - fr.CamX, hz = fr.RiderZ - fr.CamZ, hl = Math.Sqrt(hx * hx + hz * hz);
            float lx = (float)(fr.RiderX + hx / hl * 25 - home.X0), lz = (float)(fr.RiderZ + hz / hl * 25 - home.Z0);
            string dir = "b0flicker/ctx_" + home.Tx + "_" + home.Ty;
            if (!File.Exists(Path.Combine(ObjDump.Dir, dir, "terrain.obj")))
            {
                var terrain = new MeshData();
                TerrainMesher.Build(src, home, new TerrainOptions { Step = 1 }, terrain);
                var roads = new MeshData();
                RoadMesher.Build(src, sampler, new RoadOptions(), roads);
                ObjDump.Write(terrain, dir + "/terrain.obj");
                ObjDump.Write(roads, dir + "/roads.obj");
            }
            TestContext.WriteLine("{0}: t {1:0.00} s; context {2}; eye {3:0.0},{4:0.0},{5:0.0} look {6:0.0},{7:0.0},{8:0.0}; B0 {9} tris, B1 {10}, standing in {11}", name, fr.T, dir, ex,
                                  gy + 2.6, ez, lx, ry + 1.2, lz, scene.B0.TriangleCount, scene.B1.TriangleCount, scene.StandIn.TriangleCount);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Tests

        /// <summary>The street, the ride and the camera are what the owner did: a real old-core street at riding speed.</summary>
        [Test]
        public void TheRideFollowsARealOldCoreStreet()
        {
            Street st = ThamelToAsan();
            TestContext.WriteLine("Thamel Chowk to Asan: {0:0} m along {1}", st.Length, st.Names);
            Assert.That(st.Length, Is.InRange(900.0, 1500.0));
            Assert.That(st.Names, Does.Contain("Thahity Jyatha Marg"));
            List<RideFrame> frames = Ride(st, 10.0);
            int stopped = 0;
            double sway = 0;
            foreach (RideFrame f in frames)
            {
                if (f.Stopped) stopped++;
                sway = Math.Max(sway, Math.Sqrt((f.CamX - f.RiderX) * (f.CamX - f.RiderX) + (f.CamZ - f.RiderZ) * (f.CamZ - f.RiderZ)));
            }
            Assert.That(stopped, Is.EqualTo(3 * 150).Within(3));
            Assert.That(sway, Is.InRange(4.0, 6.0), "a chase camera a few metres behind the rider");
        }

        /// <summary>
        /// The band centre (WorldStreamer: BandAnchor) follows the rider, not the chase camera: while the rider stands and
        /// the camera swings ±40° round it (3 m sideways) the centre does not move at all; while riding it trails the rider
        /// by at most the dead zone and never jitters with the camera's sway; a camera far from the focus (free flight, a
        /// framed shot) is followed instead, and a teleport jumps.
        /// </summary>
        [Test]
        public void TheBandCentreFollowsTheRiderNotTheSwayingCamera()
        {
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            // The same ride twice: with the swaying, swinging chase camera, and with a camera fixed 4.5 m behind the rider
            // along the street. The band centre must be the same in every frame.
            Street st = ThamelToAsan();
            var swaying = new BandAnchor();
            var steady = new BandAnchor();
            double stillMoves = 0, worstLag = 0, camSwing = 0, worstDiff = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                RideFrame f = frames[i];
                double ox = swaying.X, oz = swaying.Z;
                swaying.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                double hx = f.RiderX - f.CamX, hz = f.RiderZ - f.CamZ, hl = Math.Sqrt(hx * hx + hz * hz);
                steady.Follow(f.RiderX - 4.5 * hx / hl, f.RiderZ - 4.5 * hz / hl, f.RiderX, f.RiderZ, true);
                worstDiff = Math.Max(worstDiff, Math.Sqrt(Math.Pow(swaying.X - steady.X, 2) + Math.Pow(swaying.Z - steady.Z, 2)));
                if (i > 0 && f.Stopped && frames[i - 1].Stopped)
                {
                    stillMoves = Math.Max(stillMoves, Math.Sqrt(Math.Pow(swaying.X - ox, 2) + Math.Pow(swaying.Z - oz, 2)));
                    camSwing = Math.Max(camSwing, Math.Sqrt(Math.Pow(f.CamX - frames[i - 1].CamX, 2) + Math.Pow(f.CamZ - frames[i - 1].CamZ, 2)));
                }
                worstLag = Math.Max(worstLag, Math.Sqrt(Math.Pow(swaying.X - f.RiderX, 2) + Math.Pow(swaying.Z - f.RiderZ, 2)));
            }
            TestContext.WriteLine("standing: the camera swings up to {0:0.00} m a frame, the centre moves {1:0.000} m; riding: {2:0.00} m behind the rider at most; " +
                                  "swaying and steady cameras give centres {3:0.000} m apart at most ({4:0} m of street)", camSwing, stillMoves, worstLag, worstDiff, st.Length);
            Assert.Greater(camSwing, 0.05, "the camera swings while the rider stands");
            Assert.AreEqual(0.0, stillMoves, 1e-9, "the band centre stands with the rider");
            Assert.LessOrEqual(worstLag, BandAnchor.DeadzoneM + 1e-6);
            Assert.AreEqual(0.0, worstDiff, 1e-9, "the camera's sway never reaches the band centre");

            var a = new BandAnchor();
            a.Follow(100, 100, 0, 0, true);
            Assert.AreEqual(100.0, a.X, "a camera 141 m from the focus is followed itself (free flight, framed shots)");
            a.Follow(100.5, 100, 0, 0, true);
            Assert.AreEqual(100.0, a.X, "within the dead zone");
            a.Follow(100, 100, 90, 100, true);
            Assert.AreEqual(91.0, a.X, 1e-9, "a chase camera: the focus, trailing by the dead zone");
            a.Follow(1000, 1000, 1000, 1000, true);
            Assert.AreEqual(1000.0, a.X, "a teleport jumps");
            a.Follow(5, 5, 0, 0, false);
            Assert.AreEqual(5.0, a.X, "no focus: the camera");
        }

        /// <summary>
        /// The repro of the owner's flicker: the old ring cells on the ride. A B1 block stands in (its B0 houses replaced
        /// by extrusions) whenever any cell it holds is still building, so whole blocks flip between boxes and houses as
        /// the ring advances, and a house at the near ring's radius is drawn twice (its near and lite forms dithered
        /// together), its seam sweeping with the camera.
        /// </summary>
        [Test]
        public void OldRingCellsFlickerOnTheRide()
        {
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            for (int tier = 0; tier <= 2; tier++)
            {
                Tally old = OldRings.Ride(tier, frames);
                TestContext.WriteLine("{0,-4} old rings: {1}; first flip {2}; first gap {3}", TierName[tier], old, old.FirstFlip, old.FirstGap);
                Assert.Greater(old.BlockFlips, 0, TierName[tier] + ": the old rings flip blocks while riding");
                Assert.Greater(old.DoubleFrames + old.GapFrames, 0, TierName[tier] + ": and draw houses twice or not at all");
            }
        }

        /// <summary>
        /// The B0 cells on the same ride, per tier: no gap, no double, no block flip, no cell shown again after hiding,
        /// no level switching back and forth, no rebuild while the rider stands still, and every shown cell's colliders
        /// registered exactly; the planned hand-overs (B0/B1 cross-fade, one level change on the way in or out) only.
        /// </summary>
        [Test]
        public void B0CellsStayStableOnTheRide()
        {
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            var tallies = new Tally[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                var anchor = new BandAnchor();
                tallies[tier] = RideCells(tier, frames, f =>
                {
                    anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                    return new[] { anchor.X, anchor.Z };
                });
                Tally t = tallies[tier];
                TestContext.WriteLine("{0,-4} cells: {1}; first gap {2}; first double {3}; first flip {4}", TierName[tier], t, t.FirstGap, t.FirstDouble, t.FirstFlip);
                TestContext.WriteLine("     peak: {0}; first reversal {1}; first re-show {2}", t.PeakNote, t.FirstReversal, t.FirstReshow);
                TestContext.WriteLine("     worker: {0}", t.Work);
            }
            for (int tier = 0; tier <= 2; tier++) AssertStable(tallies[tier], TierName[tier]);
        }

        /// <summary>
        /// The same on the main roads by car at 50 km/h (Lainchaur, Kantipath, Durbar Marg, Maitighar), the old ring cells
        /// first: the lead (BandConfig.B0NeedLeadS and B0BuildLeadS of the centre's velocity, capped at
        /// B0CellSet.MaxLeadM) keeps the band built ahead at speed, so no B1 block stands in and no house is missing or
        /// drawn twice.
        /// </summary>
        [Test]
        public void B0CellsStayStableDrivingKantipathAt50()
        {
            Street st = LainchaurToMaitighar();
            TestContext.WriteLine("Lainchaur to Maitighar: {0:0} m along {1}", st.Length, st.Names);
            Assert.That(st.Length, Is.InRange(2000.0, 4000.0));
            Assert.That(st.Names, Does.Contain("Durbar Marg"));
            List<RideFrame> frames = Ride(st, 50.0 / 3.6);
            var tallies = new Tally[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                Tally old = OldRings.Ride(tier, frames);
                TestContext.WriteLine("{0,-4} old rings: {1}", TierName[tier], old);
                var anchor = new BandAnchor();
                tallies[tier] = RideCells(tier, frames, f =>
                {
                    anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                    return new[] { anchor.X, anchor.Z };
                });
                Tally t = tallies[tier];
                TestContext.WriteLine("{0,-4} cells:     {1}; first gap {2}; first double {3}; first flip {4}", TierName[tier], t, t.FirstGap, t.FirstDouble, t.FirstFlip);
                TestContext.WriteLine("     peak: {0}; first re-show {1}; first reversal {2}", t.PeakNote, t.FirstReshow, t.FirstReversal);
                TestContext.WriteLine("     worker: {0}", t.Work);
                Assert.Greater(old.BlockFlips + old.DoubleFrames + old.GapFrames, 0, TierName[tier] + ": the old rings flicker on the main road too");
            }
            for (int tier = 0; tier <= 2; tier++) AssertStable(tallies[tier], TierName[tier]);
        }

        /// <summary>
        /// A rider rolling to and fro on Thahity Jyatha Marg (30 m each way at 5 m/s, six legs, the chase camera swinging
        /// round at each turn): the cells' hysteresis (B0CellSet.ExitPadM beyond the build pad, BandConfig.B0LevelHysteresisM
        /// on levels) keeps every cell shown and every level put, so nothing pops in or out as the rider dithers.
        /// </summary>
        [Test]
        public void B0CellsStayStableRollingToAndFro()
        {
            Street st = ThamelToAsan();
            List<RideFrame> frames = Shuttle(st, 0.75 * st.Length, 30.0, 5.0, 6);
            var tallies = new Tally[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                Tally old = OldRings.Ride(tier, frames);
                var anchor = new BandAnchor();
                tallies[tier] = RideCells(tier, frames, f =>
                {
                    anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                    return new[] { anchor.X, anchor.Z };
                });
                TestContext.WriteLine("{0,-4} old rings: {1}", TierName[tier], old);
                TestContext.WriteLine("{0,-4} cells:     {1}; first re-show {2}; first reversal {3}; peak {4}", TierName[tier], tallies[tier], tallies[tier].FirstReshow,
                                      tallies[tier].FirstReversal, tallies[tier].PeakNote);
            }
            for (int tier = 0; tier <= 2; tier++) AssertStable(tallies[tier], TierName[tier]);
        }

        /// <summary>
        /// The "stutters" half of owner playtest #4: the B0 cells' main-thread work per frame on the Thamel to Asan ride
        /// (selection with its hysteresis, the stand-in fixed point, show and hide, collider registration, the upload
        /// bookkeeping and eviction; the builds run on a worker, here after the frame) stays a small slice of a frame on
        /// every tier, and the ride is as stable with the builds off the frame as with them inline.
        /// </summary>
        [Test]
        public void B0MainThreadWorkPerFrameIsSmall()
        {
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0);
            for (int tier = 0; tier <= 2; tier++)
            {
                var anchor = new BandAnchor();
                var ms = new List<double>(frames.Count);
                Tally t = RideCells(tier, frames, f =>
                {
                    anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                    return new[] { anchor.X, anchor.Z };
                }, (f, b, c) => ms.Add(b.MainMs), true);
                var slow = new List<string>();
                for (int f = 0; f < ms.Count; f++)
                    if (ms[f] > 2.0 && slow.Count < 6) slow.Add(f + ": " + ms[f].ToString("0.0"));
                int over = 0;
                foreach (double m in ms)
                    if (m > 2.0) over++;
                ms.Sort();
                double sum = 0;
                foreach (double m in ms) sum += m;
                double p50 = ms[ms.Count / 2], p99 = ms[(int)(ms.Count * 0.99)], max = ms[ms.Count - 1];
                TestContext.WriteLine("{0,-4} main thread per frame: mean {1:0.000} ms, median {2:0.000}, p99 {3:0.000}, max {4:0.000} ({5} frames, {6} over 2 ms: {7}); {8}",
                                      TierName[tier], sum / ms.Count, p50, p99, max, ms.Count, over, string.Join(", ", slow), t);
                AssertStable(t, TierName[tier]);
                // Wall-clock times on a shared test machine: the median and the mean are asserted (preemption by other
                // processes shows in the tail, which is reported).
                Assert.Less(p50, 0.5, TierName[tier] + ": B0's main-thread work is a sliver of the frame");
                Assert.Less(sum / ms.Count, 1.0, TierName[tier] + ": on average too");
            }
        }

        /// <summary>
        /// The rides with tiles streaming in every frame and taking all the upload room they are left (the worst the
        /// streamer gives B0, WorldStreamer.Tick through <see cref="B0UploadShare"/>): Thamel to Asan at 10 m/s and
        /// Kantipath at 50 km/h. Before this round B0 went first only after three starved frames, a level build waiting
        /// for upload held back the build of a missing house (MaxReady) and uploads went first come, first served: a house
        /// riding brings in reached the band before its build was up, so its B1 block stood in and the whole block popped
        /// to boxes and back (Thamel to Asan: 36 / 160 / 116 block flips and 13 / 316 / 70 cell re-shows on Low / Mid /
        /// High). Now a build the band needs now (<see cref="B0CellSet.UrgentUploadWaiting"/>) is started past waiting
        /// level builds, uploaded first and before the tiles: the rides are as stable as with the whole cap, and B0 takes
        /// at most half the cap in a frame. The streamer's old policy (starved frames only) is ridden for comparison.
        /// </summary>
        [Test]
        public void B0CellsStayStableWhileTilesStreamIn()
        {
            List<RideFrame> frames = Ride(ThamelToAsan(), 10.0), drive = Ride(LainchaurToMaitighar(), 50.0 / 3.6);
            var tallies = new List<(string name, Tally t)>();
            for (int tier = 0; tier <= 2; tier++)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    var anchor = new BandAnchor();
                    Tally t = RideCells(tier, pass == 2 ? drive : frames, f =>
                    {
                        anchor.Follow(f.CamX, f.CamZ, f.RiderX, f.RiderZ, true);
                        return new[] { anchor.X, anchor.Z };
                    }, null, false, true, pass > 0);
                    string name = TierName[tier] + (pass == 0 ? " Thamel-Asan, starved-only" : pass == 1 ? " Thamel-Asan, urgent-first" : " Kantipath at 50, urgent-first");
                    TestContext.WriteLine("{0}: {1}; first flip {2}; first gap {3}", name, t, t.FirstFlip, t.FirstGap);
                    TestContext.WriteLine("     worker: {0}", t.Work);
                    if (pass > 0) tallies.Add((name, t));
                }
            }
            foreach ((string name, Tally t) in tallies)
            {
                AssertStable(t, name.Substring(0, name.IndexOf(' ')));
                Assert.LessOrEqual(t.MostUploadBytes, 1L << 19, name + ": B0 takes at most half the frame's cap");
            }
        }

        private static void AssertStable(Tally t, string tier)
        {
            Assert.AreEqual(0, t.GapFrames, tier + ": no frame shows a hole in a house (" + t.FirstGap + ")");
            Assert.AreEqual(0, t.DoubleFrames, tier + ": no frame draws a house twice (" + t.FirstDouble + ")");
            Assert.AreEqual(0, t.BlockFlips, tier + ": no B1 block flips while riding (" + t.FirstFlip + ")");
            Assert.AreEqual(0, t.Reshows, tier + ": no cell shows again after it hid (" + t.FirstReshow + ")");
            Assert.AreEqual(0, t.LevelReversals, tier + ": no house switches level back and forth (" + t.FirstReversal + ")");
            Assert.AreEqual(0, t.StopBuilds, tier + ": nothing is rebuilt while the rider stands still");
            Assert.AreEqual(0, t.ColliderMismatches, tier + ": colliders match what is drawn");
            int k = Array.IndexOf(TierName, tier);
            Assert.LessOrEqual(t.PeakShownTris, BandConfig.B0ShownBudgetTris(k), tier + ": the ride stays within the B0 budget (" + t.PeakNote + ")");
        }
    }
}
