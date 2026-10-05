using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V1 (width model) and V2 (no ribbon over buildings), plus seams, roundabouts, junction
    /// caps and markings on the W2 sample pack.</summary>
    public class MeshingRoadWidthTests
    {
        private static readonly TileId A = new TileId(10, 516, 161);

        private static RoadRecord Road(RoadClass c, ulong widthCm, RoadFlags flags, params int[] pointsCm)
        {
            return new RoadRecord { OsmWayId = 77, RoadClass = c, Surface = Surface.Asphalt, WidthCm = widthCm, Flags = flags, Points = pointsCm };
        }

        private static RoadAttrRecord Attr(AreaType area, RoadAttrFlags flags = RoadAttrFlags.None, params int[] corridorDm)
        {
            return new RoadAttrRecord { Area = area, Flags = (byte)flags, CorridorDm = corridorDm };
        }

        [Test]
        public void RealWidthDefaultsFollowTheClassByAreaTable()
        {
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Trunk, AreaType.Urban, false, 0), Is.EqualTo(14f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Trunk, AreaType.Rural, false, 0), Is.EqualTo(9f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Trunk, AreaType.Hill, false, 0), Is.EqualTo(7.5f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Trunk, AreaType.Urban, true, 0), Is.EqualTo(7f), "per carriageway");
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Primary, AreaType.Urban, false, 4), Is.EqualTo(14f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Primary, AreaType.Urban, false, 0), Is.EqualTo(7f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Secondary, AreaType.OldCore, false, 0), Is.EqualTo(6f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Tertiary, AreaType.OldCore, false, 0), Is.EqualTo(5f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Residential, AreaType.OldCore, false, 0), Is.EqualTo(4f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Residential, AreaType.Urban, false, 0), Is.EqualTo(5f));
            Assert.That(RoadWidthModel.DefaultRealWidthM(RoadClass.Footway, AreaType.OldCore, false, 0), Is.EqualTo(1.75f));
            Assert.That(RoadWidthModel.ShoulderM(RoadClass.Trunk, AreaType.Rural), Is.EqualTo(1.5f));
            Assert.That(RoadWidthModel.ShoulderM(RoadClass.Secondary, AreaType.Hill), Is.EqualTo(0.75f));
            Assert.That(RoadWidthModel.ShoulderM(RoadClass.Primary, AreaType.Urban), Is.EqualTo(0f));
            // Tags: plausible ones win, implausible fall back to the table; always clamped to [floor, 40].
            var urban = Attr(AreaType.Urban);
            Assert.That(RoadWidthModel.RealWidthM(Road(RoadClass.Primary, 1250, 0, 0, 0, 1000, 0), urban), Is.EqualTo(12.5f));
            Assert.That(RoadWidthModel.RealWidthM(Road(RoadClass.Primary, 100, 0, 0, 0, 1000, 0), urban), Is.EqualTo(7f), "1 m primary is a bad tag");
            Assert.That(RoadWidthModel.RealWidthM(Road(RoadClass.Primary, 9000, 0, 0, 0, 1000, 0), urban), Is.EqualTo(7f), "90 m is a bad tag");
            Assert.That(RoadWidthModel.RealWidthM(Road(RoadClass.Primary, 300, 0, 0, 0, 1000, 0), urban), Is.EqualTo(5f), "floor");
            Assert.That(RoadWidthModel.AreaOf(new RoadAttrRecord()), Is.EqualTo(AreaType.Urban));
        }

        [Test]
        public void GameWidthIsScaledWithMinimumsAndTheRealPlusSixCap()
        {
            var urban = Attr(AreaType.Urban);
            var core = Attr(AreaType.OldCore);
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Residential, 0, 0, 0, 0, 1000, 0), urban), Is.EqualTo(6.25f).Within(1e-4));
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Residential, 250, 0, 0, 0, 1000, 0), urban), Is.EqualTo(4f), "minimum 4 m");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Primary, 500, 0, 0, 0, 1000, 0), urban), Is.EqualTo(6.5f), "major minimum");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Primary, 3000, 0, 0, 0, 1000, 0), urban), Is.EqualTo(36f), "real + 6");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Primary, 500, RoadFlags.Oneway, 0, 0, 1000, 0), urban), Is.EqualTo(6.25f).Within(1e-4));
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Primary, 500, RoadFlags.Oneway, 0, 0, 1000, 0), Attr(AreaType.Urban, RoadAttrFlags.Dual)),
                        Is.EqualTo(7f), "dual minimum");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Footway, 200, 0, 0, 0, 1000, 0), urban), Is.EqualTo(2f), "footways unscaled");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Footway, 100, 0, 0, 0, 1000, 0), core), Is.EqualTo(1.2f), "old-core footway minimum");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Steps, 100, 0, 0, 0, 1000, 0), urban), Is.EqualTo(1.2f));
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Pedestrian, 600, 0, 0, 0, 1000, 0), urban), Is.EqualTo(6.9f).Within(1e-4), "x 1.15");
            Assert.That(RoadWidthModel.NominalGameWidthM(Road(RoadClass.Track, 200, 0, 0, 0, 1000, 0), core), Is.EqualTo(3f));
        }

        [Test]
        public void TheCorridorClampNeverGoesUnderTheRealWidth()
        {
            // Tagged: never under the tag. Untagged: the class default is an estimate, the measured limit wins down
            // to the floor.
            Assert.That(RoadWidthModel.ClampToLimit(4f, 5f, 2.5f, true, 3f), Is.EqualTo(4f));
            Assert.That(RoadWidthModel.ClampToLimit(4f, 5f, 2.5f, false, 3f), Is.EqualTo(3f));
            Assert.That(RoadWidthModel.ClampToLimit(4f, 5f, 2.5f, false, 1f), Is.EqualTo(2.5f));
            Assert.That(RoadWidthModel.ClampToLimit(4f, 5f, 2.5f, true, 4.6f), Is.EqualTo(4.6f));
            Assert.That(RoadWidthModel.ClampToLimit(4f, 5f, 2.5f, true, float.PositiveInfinity), Is.EqualTo(5f));
            // The limit is the corridor minus 1 m clearance, a 30 m moving minimum over 20 m samples; open = +inf.
            var a = Attr(AreaType.Urban, RoadAttrFlags.None, 0, 120, 0, 0, 90);
            Assert.That(RoadWidthModel.LimitAt(a, 5f, 0), Is.EqualTo(float.PositiveInfinity));
            Assert.That(RoadWidthModel.LimitAt(a, 5f, 20), Is.EqualTo(11f).Within(1e-4));
            Assert.That(RoadWidthModel.LimitAt(a, 5f, 40), Is.EqualTo(float.PositiveInfinity));
            Assert.That(RoadWidthModel.LimitAt(a, 5f, 75), Is.EqualTo(8f).Within(1e-4));
            Assert.That(RoadWidthModel.LimitAt(Attr(AreaType.Urban), 5f, 10), Is.EqualTo(float.PositiveInfinity));
            Assert.That(RoadWidthModel.LimitAt(Attr(AreaType.OldCore), 5f, 10), Is.EqualTo(5f), "unknown corridor in an old core = real");
        }

        [Test]
        public void WidthsTaperOneInTwentyAndCutEndsArePinned()
        {
            // 400 m tertiary (7 m real, 8.75 m nominal) squeezed to 6 m (limit 5 m -> real) around 200 m.
            var corridor = new int[21];
            for (int i = 0; i < corridor.Length; i++) corridor[i] = i == 10 ? 60 : 0;
            RoadRecord r = Road(RoadClass.Tertiary, 700, 0, 0, 0, 40000, 0);
            var p = new RoadWidthProfile();
            RoadWidthModel.BuildProfile(r, Attr(AreaType.Urban, RoadAttrFlags.None, corridor), p);
            Assert.That(p.LengthM, Is.EqualTo(400f).Within(0.01));
            Assert.That(p.MaxWidth, Is.EqualTo(8.75f).Within(1e-3));
            Assert.That(p.WidthAt(200), Is.EqualTo(7f).Within(1e-3), "a tagged road never narrows under its real width");
            for (int i = 1; i < p.Count; i++)
                Assert.That(Math.Abs(p.Width[i] - p.Width[i - 1]), Is.LessThanOrEqualTo(p.StepM / RoadWidthModel.TaperRatio + 1e-4), "1 : 20");
            // The taper reaches full width 1.75 m x 20 = 35 m beyond the clamp (which spans 185-215 m).
            Assert.That(p.WidthAt(185 - 35), Is.EqualTo(8.75f).Within(1e-3));
            Assert.That(p.WidthAt(160), Is.LessThan(8.75f));

            // A cut end (tile border) in a built-up area meets the border at the real width, then tapers out.
            RoadRecord cut = Road(RoadClass.Tertiary, 700, RoadFlags.HasPrevCtx, -1000, 0, 0, 0, 40000, 0);
            RoadWidthModel.BuildProfile(cut, Attr(AreaType.Urban), p);
            Assert.That(p.Width[0], Is.EqualTo(7f).Within(1e-4));
            Assert.That(p.WidthAt(35), Is.EqualTo(8.75f).Within(1e-3));
            RoadWidthModel.BuildProfile(cut, Attr(AreaType.Rural), p);
            Assert.That(p.Width[0], Is.EqualTo(7f).Within(1e-4), "the border width does not depend on the area type");
            Assert.That(RoadWidthModel.BorderWidthM(Road(RoadClass.Residential, 0, 0, 0, 0, 1000, 0)), Is.EqualTo(5f), "untagged: the URBAN default");
            // A corridor tighter than the border width right at the cut does not move the end.
            var tight = new int[21];
            tight[0] = 40;
            RoadWidthModel.BuildProfile(Road(RoadClass.Tertiary, 0, RoadFlags.HasPrevCtx, -1000, 0, 0, 0, 40000, 0), Attr(AreaType.Urban, RoadAttrFlags.None, tight), p);
            Assert.That(p.Width[0], Is.EqualTo(7f).Within(1e-4));
            Assert.That(RoadWidthModel.GameWidthM(r, Attr(AreaType.Urban), 10f), Is.EqualTo(8.75f).Within(1e-3));
        }

        [Test]
        public void DualCarriagewaysWidenOutwardsWithinTheOneSidedCorridor()
        {
            // A 7 m dual carriageway in a 15 m building corridor (limit 14 m, 7 m each side): all the widening and the
            // footpath go to the outer side, so outer edge + footpath must stay within 7 m of the centreline.
            RoadRecord dual = Road(RoadClass.Primary, 700, RoadFlags.Oneway, 0, 0, 30000, 0);
            var corridor = Enumerable.Repeat(150, 20).ToArray();
            RoadAttrRecord a = Attr(AreaType.Urban, RoadAttrFlags.Dual | RoadAttrFlags.Paintable, corridor);
            var prof = new RoadWidthProfile();
            RoadWidthModel.BuildProfile(dual, a, prof);
            for (float s = 0; s <= 300f; s += 10f)
            {
                RoadProfile p = RoadWidthModel.ProfileFrom(dual, a, prof, s);
                Assert.That(p.CarriagewayM, Is.GreaterThanOrEqualTo(7f - 1e-4f), "never narrower than real");
                float outer = p.CentreShiftM + 0.5f * p.CarriagewayM + p.ShoulderM + p.FootpathLeftM;
                float median = -p.CentreShiftM + 0.5f * p.CarriagewayM + p.ShoulderM + p.FootpathRightM;
                Assert.That(outer, Is.LessThanOrEqualTo(7f + 1e-3f), "outer edge and footpath at " + s);
                Assert.That(median, Is.LessThanOrEqualTo(7f + 1e-3f), "median side at " + s);
            }
        }

        [Test]
        public void CrossSectionsHaveMediansLanesFootpathsAndPaint()
        {
            RoadRecord dual = Road(RoadClass.Trunk, 0, RoadFlags.Oneway, 0, 0, 30000, 0);
            RoadProfile p = RoadWidthModel.ProfileAt(dual, Attr(AreaType.Urban, RoadAttrFlags.Dual | RoadAttrFlags.Paintable), 50f);
            Assert.That(p.MedianM, Is.GreaterThanOrEqualTo(RoadWidthModel.MinMedianM));
            Assert.That(p.LanesBwd, Is.EqualTo(0));
            Assert.That(p.FootpathRightM, Is.EqualTo(0f), "no footpath on the median side");
            Assert.That(p.CentreLine, Is.False);
            Assert.That(p.LaneLines, Is.True);
            var wide = new RoadAttrRecord { Area = AreaType.Urban, Flags = (byte)RoadAttrFlags.Dual, MedianCm = 250, CorridorDm = new int[0] };
            Assert.That(RoadWidthModel.ProfileAt(dual, wide, 50f).MedianM, Is.EqualTo(2.5f));

            RoadRecord primary = Road(RoadClass.Primary, 0, 0, 0, 0, 30000, 0);
            p = RoadWidthModel.ProfileAt(primary, Attr(AreaType.Urban), 50f);
            Assert.That(p.CentreLine && p.EdgeLines, Is.True);
            Assert.That(p.FootpathLeftM, Is.InRange(2.0f * 1.15f, 3.5f * 1.15f));
            Assert.That(p.KerbLeftM, Is.EqualTo(RoadWidthModel.KerbTopM));
            Assert.That(p.Access & Travel.Bus, Is.EqualTo(Travel.Bus));
            p = RoadWidthModel.ProfileAt(primary, Attr(AreaType.OldCore), 50f);
            Assert.That(p.CentreLine || p.EdgeLines, Is.False, "old cores carry no paint");
            Assert.That(p.FootpathLeftM + p.FootpathRightM, Is.EqualTo(0f), "shared surface");
            RoadRecord lane = Road(RoadClass.Residential, 400, 0, 0, 0, 30000, 0);
            p = RoadWidthModel.ProfileAt(lane, Attr(AreaType.Urban), 50f);
            Assert.That(p.Lanes, Is.EqualTo(1), "a two-way road under 5.5 m is one shared lane");
            Assert.That(p.CentreLine, Is.False);
            var side = new RoadAttrRecord { Area = AreaType.Rural, Sidewalk = (byte)Sidewalk.Left, CorridorDm = new int[0] };
            p = RoadWidthModel.ProfileAt(Road(RoadClass.Secondary, 0, 0, 0, 0, 30000, 0), side, 50f);
            Assert.That(p.FootpathLeftM, Is.GreaterThan(0f));
            Assert.That(p.FootpathRightM, Is.EqualTo(0f));
        }

        [Test]
        public void AccessFollowsTheGameWidth()
        {
            Assert.That(RoadWidthModel.WidthMask(1.5f), Is.EqualTo(Travel.Foot | Travel.Horse));
            Assert.That(RoadWidthModel.WidthMask(2.5f) & Travel.Motorbike, Is.EqualTo(Travel.Motorbike));
            Assert.That(RoadWidthModel.WidthMask(3.4f) & Travel.Car, Is.EqualTo(Travel.None));
            Assert.That(RoadWidthModel.WidthMask(3.5f) & Travel.Car, Is.EqualTo(Travel.Car));
            Assert.That(RoadWidthModel.WidthMask(5.9f) & Travel.Bus, Is.EqualTo(Travel.None));
            Assert.That(RoadWidthModel.WidthMask(6.0f), Is.EqualTo(TravelMasks.All));
            RoadRecord r = Road(RoadClass.Residential, 0, 0, 0, 0, 30000, 0);
            Assert.That(RoadWidthModel.AccessFor(8f, r, Attr(AreaType.Urban, RoadAttrFlags.HeritagePedestrian)),
                        Is.EqualTo(Travel.Foot | Travel.Bicycle | Travel.Horse));
            r.Access = Travel.Foot | Travel.Car;
            Assert.That(RoadWidthModel.AccessFor(8f, r, Attr(AreaType.Urban)), Is.EqualTo(Travel.Foot | Travel.Car));
            // A hairpin under 12 m radius: no bus.
            RoadRecord hairpin = Road(RoadClass.Primary, 0, 0, 0, 0, 1000, 0, 1000, 1000, 0, 1000, 0, 2000);
            Assert.That(RoadWidthModel.MinRadiusM(hairpin), Is.LessThan(RoadWidthModel.MinBusRadiusM));
            Assert.That(RoadWidthModel.AccessFor(8f, hairpin, Attr(AreaType.Urban)) & Travel.Bus, Is.EqualTo(Travel.None));
        }

        [Test]
        public void ClosedServiceLoopsOnW2TilesAreNotGuessedAsRoundabouts()
        {
            // A parking loop (closed service way, r = 15 m) on a W2 tile with RATR and no JNCT record: W2_DESIGN 4.7
            // allows only mapped rings, mini nodes and curated synthetic islands, so no island is drawn.
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300);
            const int cx = 50000, cz = 50000;
            var loop = new List<int>();
            for (int k = 0; k <= 16; k++)
            {
                double a = 2 * Math.PI * (k % 16) / 16;
                loop.Add(cx + (int)Math.Round(1500 * Math.Cos(a)));
                loop.Add(cz + (int)Math.Round(1500 * Math.Sin(a)));
            }
            t.Roads.Add(new RoadRecord { OsmWayId = 3, RoadClass = RoadClass.Service, Surface = Surface.Asphalt, Points = loop.ToArray() });
            t.RoadAttrs.Add(Attr(AreaType.Urban));
            Assert.That(t.HasRoadAttrs, Is.True);
            Assert.That(t.Junctions.Count, Is.EqualTo(0));
            Assert.That(RoadLayout.For(t).Islands, Is.Empty, "no guessed ring on a W2 tile");
        }

        [Test]
        public void JawalakhelIslandIsWithinTwoMetresOfItsMeasuredDiameter()
        {
            // Ring centreline 43.8 m (JNCT), four 6.5 m primaries: ring 8.1 + 1 m wide; island measured 36 m [O].
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300);
            const int cx = 50000, cz = 50000;
            var ring = new List<int>();
            for (int k = 0; k <= 24; k++)
            {
                double a = 2 * Math.PI * (k % 24) / 24;
                ring.Add(cx + (int)Math.Round(2190 * Math.Cos(a)));
                ring.Add(cz + (int)Math.Round(2190 * Math.Sin(a)));
            }
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Primary, Surface = Surface.Asphalt, Flags = RoadFlags.Oneway, Points = ring.ToArray() });
            t.RoadAttrs.Add(Attr(AreaType.Urban, RoadAttrFlags.RingMember | RoadAttrFlags.Paintable));
            int[,] dirs = { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
            for (int d = 0; d < 4; d++)
            {
                t.Roads.Add(Road(RoadClass.Primary, 650, 0, cx + dirs[d, 0] * 2200, cz + dirs[d, 1] * 2200, cx + dirs[d, 0] * 25000, cz + dirs[d, 1] * 25000));
                t.Roads[t.Roads.Count - 1].OsmWayId = (ulong)(10 + d);
                t.RoadAttrs.Add(Attr(AreaType.Urban, RoadAttrFlags.Paintable));
            }
            t.Junctions.Add(new JunctionRecord { OsmNodeId = 5, Kind = JunctionKind.Roundabout, Arms = 4, Flags = (byte)JunctionFlags.HasPolice, XCm = cx, ZCm = cz, RingDiameterCm = 4380 });
            RoadLayout layout = RoadLayout.For(t);
            Assert.That(layout.Islands.Count, Is.EqualTo(1));
            RoadIsland island = layout.Islands[0];
            Assert.That(island.Kind, Is.EqualTo(IslandKind.Roundabout));
            Assert.That(island.PolicePodium, Is.True);
            Assert.That(2 * island.RadiusM, Is.EqualTo(36.0).Within(2.0));
            Assert.That(island.ApronM, Is.EqualTo(1.0f));
            Assert.That(layout.HalfWidthAt(0, 10), Is.EqualTo(0.5f * (8.125f + 1f)).Within(0.01), "ring = widest approach + 1 m");
            var m = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 1), new RoadOptions(), m);
            MeshingChecks.AssertWellFormed(m, "roundabout");
            var decals = new MeshData();
            Assert.That(MarkingMesher.Build(t, new TileHeightSampler(t, 1), new RoadOptions(), decals), Is.GreaterThan(0));
            MeshingChecks.AssertWellFormed(decals, "markings");
        }

        // -----------------------------------------------------------------------------------------------------
        // Sample pack (V2, seams, caps)
        // -----------------------------------------------------------------------------------------------------

        /// <summary>Point-in-building lookups over a tile's outer rings (16 m bounding-box grid).</summary>
        private sealed class Footprints
        {
            private readonly TileData _t;
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public Footprints(TileData t)
            {
                _t = t;
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    int[] r = t.Buildings[i].Rings[0];
                    int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
                    for (int k = 0; k < r.Length / 2; k++)
                    {
                        x0 = Math.Min(x0, r[2 * k]);
                        x1 = Math.Max(x1, r[2 * k]);
                        z0 = Math.Min(z0, r[2 * k + 1]);
                        z1 = Math.Max(z1, r[2 * k + 1]);
                    }
                    for (int j = z0 / 1600; j <= z1 / 1600; j++)
                    for (int c = x0 / 1600; c <= x1 / 1600; c++)
                    {
                        long key = (long)j << 32 | (uint)c;
                        List<int> l;
                        if (!_cells.TryGetValue(key, out l)) _cells[key] = l = new List<int>();
                        l.Add(i);
                    }
                }
            }

            public int Inside(double x, double z)
            {
                List<int> l;
                if (x < 0 || z < 0 || !_cells.TryGetValue((long)(int)(z / 16) << 32 | (uint)(int)(x / 16), out l)) return -1;
                foreach (int i in l)
                    if (In(_t.Buildings[i].Rings[0], x, z)) return i;
                return -1;
            }

            private static bool In(int[] r, double x, double z)
            {
                bool c = false;
                int n = r.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = r[2 * i] / 100.0, zi = r[2 * i + 1] / 100.0, xj = r[2 * j] / 100.0, zj = r[2 * j + 1] / 100.0;
                    if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) c = !c;
                }
                return c;
            }
        }

        [Test]
        public void RibbonsNeverCoverBuildingsOnTheSamplePack()
        {
            // V2: every 2 m along every drawn road, both ribbon edges (carriageway + footpath, 0.1 m inset) lie
            // outside buildings, except where the road is already at its real width (the data overlaps; the
            // model may not narrow further). Corner geometry at vertices leaves a few hundredths of a percent.
            long samples = 0, over = 0;
            var areaSamples = new long[16];
            var areaOver = new long[16];
            foreach (TileId id in StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Buildings.Count == 0) continue;
                var fp = new Footprints(t);
                RoadLayout layout = RoadLayout.For(t);
                Assert.That(layout.FromRatr, Is.True);
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    if (!RoadWidthModel.IsMotor(r.RoadClass) && r.RoadClass != RoadClass.Pedestrian) continue;
                    if ((r.Flags & (RoadFlags.Tunnel | RoadFlags.Bridge)) != 0) continue;
                    RoadWidthProfile prof = layout.Profiles[ri];
                    for (double s = 0; s <= prof.LengthM; s += 2)
                    {
                        if (layout.InGap(ri, s)) continue;
                        RoadCut c = layout.CutAt(t, ri, s);
                        RoadProfile p = layout.ProfileAt(ri, s);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double foot = side > 0 ? p.FootpathLeftM : p.FootpathRightM;
                            double e = c.Shift + side * (c.Half + foot - 0.1);
                            samples++;
                            double bd = Math.Min(Math.Min(c.CX, c.CZ), Math.Min(t.Tile.Size - c.CX, t.Tile.Size - c.CZ));
                            int area = ((int)RoadWidthModel.AreaOf(layout.Attrs[ri]) & 7) + (bd < 35 ? 8 : 0); // + 8: seam band
                            areaSamples[area]++;
                            int b = fp.Inside(c.CX + c.UX * e, c.CZ + c.UZ * e);
                            if (b < 0 || p.CarriagewayM <= prof.RealM + 0.01f) continue;
                            double er = side * (0.5 * prof.RealM - 0.1);
                            if (fp.Inside(c.CX + c.UX * er, c.CZ + c.UZ * er) >= 0) continue; // the real road overlaps too
                            over++;
                            areaOver[area]++;
                        }
                    }
                }
            }
            Assert.That(samples, Is.GreaterThan(100000));
            Assert.That((double)over / samples, Is.LessThan(5e-4), over + " of " + samples + " edge samples over a building");
            // Per area type too (an aggregate hides a whole class going over, e.g. old-core lanes at tile seams).
            // Per area type, inside tiles and in the 35 m seam bands (an aggregate hides a whole class going over, e.g.
            // old-core lanes at tile seams). Near a seam pieces are floored at the shared border width (URBAN column for
            // untagged ways, both tiles must agree), which leaves old-core and hill seam bands above the interior rate
            // until the pipeline writes a per-cut width (open issue, World/README.md).
            for (int k = 0; k < 16; k++)
            {
                if (areaSamples[k] < 2000) continue;
                string what = (AreaType)(k & 7) + (k >= 8 ? " (seam band)" : "");
                TestContext.Progress.WriteLine("V2 " + what + ": " + areaOver[k] + " / " + areaSamples[k]);
                Assert.That((double)areaOver[k] / areaSamples[k], Is.LessThan(k >= 8 ? 1e-2 : 2e-3), what + ": " + areaOver[k] + " of " + areaSamples[k] + " edge samples over a building");
            }
        }

        [Test]
        public void JunctionCapsNeverCoverCornerBuildings()
        {
            // V2 for caps (G7): a cap vertex inside a building is allowed only where an arm's real-width strip covers the
            // building too (the data overlaps); the game-width kerb fillets shrink clear of corner houses.
            int caps = 0, bad = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Buildings.Count == 0) continue;
                var fp = new Footprints(t);
                RoadLayout layout = RoadLayout.For(t);
                foreach (JunctionCap c in layout.Caps)
                {
                    caps++;
                    for (int v = 0; v < c.Count; v++)
                    {
                        // Probe 0.2 m toward the node: a vertex exactly on a footprint edge is not a cover.
                        double dx = c.X - c.PolyX[v], dz = c.Z - c.PolyZ[v], l = Math.Sqrt(dx * dx + dz * dz);
                        if (l < 0.3) continue;
                        double x = c.PolyX[v] + dx / l * 0.2, z = c.PolyZ[v] + dz / l * 0.2;
                        if (fp.Inside(x, z) < 0) continue;
                        bool real = false;
                        for (int a = 0; a < c.ArmRoads.Length && !real; a++)
                            real = DistanceToRoad(t.Roads[c.ArmRoads[a]], x, z) <= 0.5 * layout.Profiles[c.ArmRoads[a]].RealM + 0.05;
                        if (real) continue;
                        bad++;
                        break;
                    }
                }
            }
            Assert.That(caps, Is.GreaterThan(1000));
            Assert.That((double)bad / caps, Is.LessThan(0.005), bad + " of " + caps + " caps cover a building outside the real road");
        }

        private static double DistanceToRoad(RoadRecord r, double x, double z)
        {
            double best = double.MaxValue;
            int[] p = r.Points;
            for (int k = 0; k + 1 < p.Length / 2; k++)
            {
                double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
                double f = len2 > 1e-12 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / len2)) : 0;
                double ex = ax + dx * f - x, ez = az + dz * f - z;
                best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez));
            }
            return best;
        }

        [Test]
        public void BorderCutsMeetAtTheSameWidthInBothTiles()
        {
            var tiles = StreamingSampleRegion.TilesAt(10).ToDictionary(i => i, i => StreamingSampleRegion.Tile(i));
            int pairs = 0;
            foreach (KeyValuePair<TileId, TileData> kv in tiles)
            {
                TileId id = kv.Key;
                foreach (TileId nb in new[] { new TileId(10, id.Tx + 1, id.Ty), new TileId(10, id.Tx, id.Ty + 1) })
                {
                    TileData tb;
                    if (!tiles.TryGetValue(nb, out tb)) continue;
                    TileData ta = kv.Value;
                    RoadLayout la = RoadLayout.For(ta), lb = RoadLayout.For(tb);
                    double ox = nb.X0 - id.X0, oz = nb.Z0 - id.Z0;
                    for (int i = 0; i < ta.Roads.Count; i++)
                    {
                        RoadRecord r = ta.Roads[i];
                        if (!r.HasPrevContext && !r.HasNextContext) continue;
                        for (int end = 0; end < 2; end++)
                        {
                            if (end == 0 ? !r.HasPrevContext : !r.HasNextContext) continue;
                            int k = end == 0 ? 1 : r.PointCount - 2;
                            double x = r.Points[2 * k] / 100.0 - ox, z = r.Points[2 * k + 1] / 100.0 - oz;
                            float wa = la.Profiles[i].WidthAt(end == 0 ? 0 : la.Profiles[i].LengthM);
                            for (int j = 0; j < tb.Roads.Count; j++)
                            {
                                RoadRecord q = tb.Roads[j];
                                if (q.OsmWayId != r.OsmWayId || q.RoadClass != r.RoadClass) continue;
                                for (int qe = 0; qe < 2; qe++)
                                {
                                    if (qe == 0 ? !q.HasPrevContext : !q.HasNextContext) continue;
                                    int qk = qe == 0 ? 1 : q.PointCount - 2;
                                    if (Math.Abs(q.Points[2 * qk] / 100.0 - x) > 0.05 || Math.Abs(q.Points[2 * qk + 1] / 100.0 - z) > 0.05) continue;
                                    float wb = lb.Profiles[j].WidthAt(qe == 0 ? 0 : lb.Profiles[j].LengthM);
                                    Assert.That(wb, Is.EqualTo(wa).Within(0.02), "way " + r.OsmWayId + " across " + id + " / " + nb);
                                    pairs++;
                                }
                            }
                        }
                    }
                }
            }
            Assert.That(pairs, Is.GreaterThan(50));
        }

        [Test]
        public void JunctionCapsIslandsAndMarkingsOnTheDensestTileAreWellFormed()
        {
            TileData t = StreamingSampleRegion.Tile(A);
            RoadLayout layout = RoadLayout.For(t);
            Assert.That(layout.Caps.Count, Is.GreaterThan(20));
            foreach (JunctionCap c in layout.Caps)
            {
                Assert.That(c.Count, Is.GreaterThanOrEqualTo(3));
                Assert.That(c.X, Is.InRange(RoadLayout.CapBorderMarginM, t.Tile.Size - RoadLayout.CapBorderMarginM));
                foreach (double sb in c.ArmSetback.Take(c.Count)) Assert.That(sb, Is.LessThanOrEqualTo(RoadLayout.MaxSetbackM + 1e-6));
            }
            Assert.That(layout.Islands.Any(i => i.Kind == IslandKind.Roundabout), Is.True, "the JNCT roundabout");
            var h = new TileHeightSampler(t, 2);
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, h, new RoadOptions(), m), Is.GreaterThan(100));
            MeshingChecks.AssertWellFormed(m, "roads");
            var decals = new MeshData();
            Assert.That(MarkingMesher.Build(t, h, new RoadOptions(), decals), Is.GreaterThan(0));
            MeshingChecks.AssertWellFormed(decals, "markings");
            var caps = new MeshData();
            JunctionMesher.Build(t, h, new RoadOptions(), caps, new MeshData());
            MeshingChecks.AssertWellFormed(caps, "caps");
            Assert.That(caps.TriangleCount, Is.GreaterThan(0));
            // Deterministic: the same tile meshes to the same bytes.
            var again = new MeshData();
            RoadMesher.Build(t, h, new RoadOptions(), again);
            Assert.That(again.TriangleCount, Is.EqualTo(m.TriangleCount));
            Assert.That(again.Positions.Take(again.VertexCount * 3), Is.EqualTo(m.Positions.Take(m.VertexCount * 3)));
        }
    }
}
