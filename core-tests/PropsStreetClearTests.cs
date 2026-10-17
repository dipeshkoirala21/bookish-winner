using System;
using System.Collections.Generic;
using System.Globalization;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.World.Instancing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #7 (docs/W2_PLAYTEST_1.md): "Some of the traffic poles, road signs and electric poles are in the
    /// middle of the roads/intersection. Traffic lights should be hanging on top. Electric poles should either be in the
    /// middle of the roads where dividers are, or in the footpath and road corner." Over every level-10 tile of the
    /// kathmandu_core sample pack, measured against what the road mesher draws (<see cref="RoadSurfaceQuery"/>: the
    /// widened carriageways, junction caps and roundabout rings, not the OSM centrelines):
    /// <list type="bullet">
    /// <item>no prop stands with its base on a motor carriageway, junction cap or ring (medians, footpaths, shoulders and
    /// verges are fine);</item>
    /// <item>no collider of a street prop (the dressing set the streamer registers, and the tile solids of the ground
    /// query) blocks a body standing on a motor carriageway, junction cap or ring;</item>
    /// <item>every traffic signal hangs its head over the lanes of its approach: above the overhead clearance, over the
    /// carriageway on the approaching traffic's own (left-hand) side, facing that traffic, from a pole outside the
    /// carriageway on the traffic's left.</item>
    /// </list>
    /// </summary>
    public class PropsStreetClearTests
    {
        private sealed class Sample
        {
            public TileId Id;
            public TileData Tile;
            public List<StreetProp> Props;
            public RoadSurfaceQuery Surface;
        }

        private static List<Sample> _samples;

        private static List<Sample> Samples()
        {
            if (_samples != null) return _samples;
            var list = new List<Sample>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                TileHeightSampler h = TileHeightSampler.ForArea(t, id, 1);
                var props = new List<StreetProp>();
                PropPlacement.Place(t, h, props);
                list.Add(new Sample { Id = id, Tile = t, Props = props, Surface = RoadSurfaceQuery.For(t, h, new RoadOptions()) });
            }
            return _samples = list;
        }

        private static string Where(Sample s, double lx, double lz, string what)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(s.Id.X0 + lx, s.Id.Z0 + lz, out lon, out lat);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} at {2:0.000000},{3:0.000000}", s.Id, what, lon, lat);
        }

        /// <summary>The drawn motor carriageway (ribbon, junction cap or ring) under game (x, z) for a body near
        /// <paramref name="nearY"/>; footways, paths and pedestrian streets do not count.</summary>
        private static bool OnMotorCarriageway(Sample s, double gx, double gz, float nearY, out RoadSurfaceHit hit)
        {
            if (!s.Surface.TrySample(gx, gz, nearY, out hit) || !hit.OnCarriageway) return false;
            return hit.Road < 0 || RoadWidthModel.IsMotor(s.Tile.Roads[hit.Road].RoadClass);
        }

        /// <summary>Flat markings and the gate (traffic passes between its pillars: its colliders are checked).</summary>
        private static bool HasNoStandingBase(StreetPropKind k)
        {
            return k == StreetPropKind.Helipad || k == StreetPropKind.ParkingPosition || k == StreetPropKind.Gate;
        }

        [Test]
        public void NoPropStandsOnACarriagewayJunctionOrRing()
        {
            int total = 0, bad = 0;
            var byKind = new Dictionary<StreetPropKind, int>();
            var examples = new List<string>();
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (HasNoStandingBase(p.Kind)) continue;
                    total++;
                    RoadSurfaceHit hit;
                    if (!OnMotorCarriageway(s, s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 0.5f, out hit) || Math.Abs(hit.Y - p.Y) > 1.0f) continue;
                    bad++;
                    int n;
                    byKind.TryGetValue(p.Kind, out n);
                    byKind[p.Kind] = n + 1;
                    if (examples.Count < 15) examples.Add(Where(s, p.X, p.Z, p.Kind + (p.Osm ? " (OSM)" : "") + (hit.Road < 0 ? " on a junction cap or ring" : " on road " + hit.Road)));
                }
            TestContext.WriteLine("props with a base " + total + ", on a motor carriageway, junction or ring " + bad);
            foreach (var kv in byKind) TestContext.WriteLine("  " + kv.Key + ": " + kv.Value);
            foreach (string e in examples) TestContext.WriteLine(e);
            Assert.That(total, Is.GreaterThan(5000));
            Assert.That(bad, Is.EqualTo(0), "props standing in the drawn road");
        }

        /// <summary>The street-prop colliders of a sample tile in game metres: the dressing set (every placed prop through
        /// <see cref="TileInstances.AppendColliders"/>) and the cylinders of the ground query's own tile solids that stand on
        /// a street-prop record (trees are the nature package's).</summary>
        private static void PropColliders(Sample s, List<ColliderCylinder> cyl, List<OrientedBox> boxes)
        {
            var inst = new TileInstances();
            inst.Props.AddRange(s.Props);
            var c = new StructureColliders();
            inst.AppendColliders(c);
            foreach (ColliderCylinder y in c.Cylinders)
            {
                ColliderCylinder g = y;
                g.CX += s.Id.X0;
                g.CZ += s.Id.Z0;
                cyl.Add(g);
            }
            foreach (OrientedBox b in c.Boxes)
            {
                OrientedBox g = b;
                g.CX += s.Id.X0;
                g.CZ += s.Id.Z0;
                boxes.Add(g);
            }
            var raw = new HashSet<long>();
            foreach (PropRecord p in s.Tile.Props)
            {
                StreetPropKind k;
                if (p.Kind != ObjectKind.Tree && PropPlacement.KindOf(p.Kind, out k)) raw.Add(((long)p.XCm << 32) ^ (uint)p.ZCm);
            }
            var ground = new TileGroundQuery();
            ground.Add(s.Tile);
            var prims = new List<SolidPrim>();
            ground.SolidsOf(s.Tile.Tile, prims);
            foreach (SolidPrim p in prims)
            {
                if (p.Ax != p.Bx || p.Az != p.Bz || p.Group >= 0) continue;
                long xcm = (long)Math.Round((p.Ax - s.Id.X0) * 100), zcm = (long)Math.Round((p.Az - s.Id.Z0) * 100);
                if (!raw.Contains((xcm << 32) ^ (uint)zcm)) continue;
                cyl.Add(new ColliderCylinder { CX = p.Ax, CZ = p.Az, Radius = p.Radius, Bottom = p.Bottom, Top = p.Top });
            }
        }

        /// <summary>True when a body standing on the drawn motor carriageway at (x, z) would run into a solid spanning
        /// <paramref name="bottom"/>..<paramref name="top"/> there.</summary>
        private static bool BlocksTheRoad(Sample s, double gx, double gz, float bottom, float top, out RoadSurfaceHit hit)
        {
            if (!OnMotorCarriageway(s, gx, gz, bottom + 1.0f, out hit)) return false;
            float feet = hit.Y + 0.25f;
            return feet > bottom && feet < top;
        }

        [Test]
        public void NoPropColliderBlocksACarriagewayJunctionOrRing()
        {
            int shapes = 0, bad = 0;
            var examples = new List<string>();
            foreach (Sample s in Samples())
            {
                var cyl = new List<ColliderCylinder>();
                var boxes = new List<OrientedBox>();
                PropColliders(s, cyl, boxes);
                foreach (ColliderCylinder y in cyl)
                {
                    shapes++;
                    bool hitRoad = false;
                    RoadSurfaceHit hit = default(RoadSurfaceHit);
                    for (int k = -1; k < 8 && !hitRoad; k++)
                    {
                        double a = k * Math.PI / 4, r = k < 0 ? 0 : y.Radius;
                        hitRoad = BlocksTheRoad(s, y.CX + r * Math.Sin(a), y.CZ + r * Math.Cos(a), y.Bottom, y.Top, out hit);
                    }
                    if (!hitRoad) continue;
                    bad++;
                    if (examples.Count < 15)
                        examples.Add(Where(s, y.CX - s.Id.X0, y.CZ - s.Id.Z0, string.Format(CultureInfo.InvariantCulture, "cylinder r {0:0.00} {1}", y.Radius,
                                                                                              hit.Road < 0 ? "on a junction cap or ring" : "on road " + hit.Road)));
                }
                foreach (OrientedBox b in boxes)
                {
                    shapes++;
                    double c = Math.Cos(b.YawRad), sn = Math.Sin(b.YawRad);
                    bool hitRoad = false;
                    RoadSurfaceHit hit = default(RoadSurfaceHit);
                    for (int k = 0; k < 9 && !hitRoad; k++)
                    {
                        double u = k == 8 ? 0 : (k % 3 - 1) * b.HalfX, w = k == 8 ? 0 : (k / 3 - 1) * b.HalfZ;
                        hitRoad = BlocksTheRoad(s, b.CX + u * c - w * sn, b.CZ + u * sn + w * c, b.Bottom, b.Top, out hit);
                    }
                    if (!hitRoad) continue;
                    bad++;
                    if (examples.Count < 15) examples.Add(Where(s, b.CX - s.Id.X0, b.CZ - s.Id.Z0, "box " + (hit.Road < 0 ? "on a junction cap or ring" : "on road " + hit.Road)));
                }
            }
            TestContext.WriteLine("prop collider shapes " + shapes + ", blocking a motor carriageway, junction or ring " + bad);
            foreach (string e in examples) TestContext.WriteLine(e);
            Assert.That(shapes, Is.GreaterThan(5000), "the street props are solid");
            Assert.That(bad, Is.EqualTo(0), "invisible or visible walls in the drawn road");
        }

        [Test]
        public void EverySignalHeadHangsOverItsApproachFacingTheLeftHandTraffic()
        {
            int signals = 0, overLanes = 0, low = 0, offRoad = 0, inJunction = 0, wrongHalf = 0, notFacing = 0;
            var examples = new List<string>();
            var c = CultureInfo.InvariantCulture;
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    StreetPropMesher.SignalHead head;
                    if (!StreetPropMesher.TryHangingHead(p.Kind, out head)) continue;
                    signals++;
                    double scale = p.HeightM / StreetPropMesher.DefaultHeight(p.Kind);
                    double yaw = p.YawDeg * Math.PI / 180;
                    double fx = Math.Sin(yaw), fz = Math.Cos(yaw), rx = Math.Cos(yaw), rz = -Math.Sin(yaw);
                    double hx = s.Id.X0 + p.X + scale * (fx * head.ForwardM + rx * head.RightM), hz = s.Id.Z0 + p.Z + scale * (fz * head.ForwardM + rz * head.RightM);
                    // The head faces the traffic, which comes towards it: travel = -facing.
                    double fa = yaw + head.FacingDeg * Math.PI / 180;
                    double tx = -Math.Sin(fa), tz = -Math.Cos(fa);
                    float bottom = p.Y + (float)(scale * head.BottomM);
                    RoadSurfaceHit hit;
                    string what = null;
                    if (!OnMotorCarriageway(s, hx, hz, p.Y + 0.5f, out hit))
                    {
                        offRoad++;
                        what = "head over no carriageway";
                    }
                    else if (hit.Road < 0)
                    {
                        inJunction++;
                        what = "head over the junction box, not the approach";
                    }
                    else if (bottom - hit.Y < RoadClearance.MinOverheadClearanceM)
                    {
                        low++;
                        what = string.Format(c, "head {0:0.00} m over the road", bottom - hit.Y);
                    }
                    else
                    {
                        double along = tx * hit.DirX + tz * hit.DirZ;
                        RoadRecord r = s.Tile.Roads[hit.Road];
                        bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
                        // Lateral of the head towards the traffic's left (LateralM is positive left of the point order).
                        double leftOfTraffic = along >= 0 ? hit.LateralM : -hit.LateralM;
                        if (Math.Abs(along) < Math.Cos(30 * Math.PI / 180) || oneway && along < 0)
                        {
                            notFacing++;
                            what = string.Format(c, "head faces across or against the traffic (cos {0:0.00})", along);
                        }
                        else if (!oneway && leftOfTraffic < -0.3)
                        {
                            wrongHalf++;
                            what = string.Format(c, "head over the opposing lanes ({0:0.00} m right of the centre)", -leftOfTraffic);
                        }
                        else overLanes++;
                    }
                    if (what != null && examples.Count < 20) examples.Add(Where(s, p.X, p.Z, "signal" + (p.Osm ? " (OSM)" : "") + ": " + what));
                }
            TestContext.WriteLine(string.Format(c, "signals {0}: over their approach lanes {1}; over no carriageway {2}, over the junction box {3}, below {4} m {5}, facing across or against the traffic {6}, over the opposing lanes {7}",
                                                signals, overLanes, offRoad, inJunction, RoadClearance.MinOverheadClearanceM, low, notFacing, wrongHalf));
            foreach (string e in examples) TestContext.WriteLine(e);
            Assert.That(signals, Is.GreaterThan(20), "the sample's signalised junctions");
            Assert.That(overLanes, Is.EqualTo(signals), "every signal head hangs over its approach");
        }

        [Test]
        public void EveryMappedSignalHasItsJunctionSignalledFromSeveralCorners()
        {
            int mapped = 0, lost = 0, junctions = 0, single = 0;
            var examples = new List<string>();
            foreach (Sample s in Samples())
            {
                var poles = new List<StreetProp>();
                foreach (StreetProp p in s.Props)
                    if (StreetPropMesher.IsSignal(p.Kind)) poles.Add(p);
                foreach (PropRecord r in s.Tile.Props)
                {
                    if (r.Kind != ObjectKind.TrafficSignals) continue;
                    mapped++;
                    double x = r.XCm / 100.0, z = r.ZCm / 100.0;
                    int near = 0;
                    foreach (StreetProp p in poles)
                        if ((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) < 45.0 * 45.0) near++;
                    if (near == 0)
                    {
                        lost++;
                        if (examples.Count < 10) examples.Add(Where(s, x, z, "mapped signal with no signal pole within 45 m"));
                    }
                }
                // Signal poles in clusters (a junction's corners lie within 45 m of each other).
                var used = new bool[poles.Count];
                for (int i = 0; i < poles.Count; i++)
                {
                    if (used[i]) continue;
                    int n = 0;
                    for (int j = i; j < poles.Count; j++)
                        if (!used[j] && (poles[j].X - poles[i].X) * (poles[j].X - poles[i].X) + (poles[j].Z - poles[i].Z) * (poles[j].Z - poles[i].Z) < 45.0 * 45.0)
                        {
                            used[j] = true;
                            n++;
                        }
                    junctions++;
                    if (n == 1) single++;
                }
            }
            TestContext.WriteLine("mapped signals " + mapped + ", without a signal pole within 45 m " + lost + "; signal clusters " + junctions + ", of a single pole " + single);
            foreach (string e in examples) TestContext.WriteLine(e);
            Assert.That(mapped, Is.GreaterThan(20));
            Assert.That(lost, Is.EqualTo(0), "a mapped signal's junction gets its signals");
            Assert.That(single, Is.LessThanOrEqualTo(junctions / 3), "most signalised junctions are signalled from several corners");
        }
    }
}
