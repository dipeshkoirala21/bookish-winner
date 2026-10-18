using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The owner's first Unity playtest (docs/W2_PLAYTEST_1.md #6): "There are absolutely no traffics on the road. Try to
    /// show few traffics on the roads that follow the lanes properly", and #3: "On busy area, instead of parked motorcycles,
    /// bring up the actual person driving it instead. But not too many.. 2-3 motorcycles driving, so that it does not look
    /// abandoned". The traffic of the sample is run headless exactly as LifeHost runs it (the tier's settings, the player as
    /// the focus, the camera's view for the out-of-view spawns) at real places on a main road, a busy bazaar and quiet side
    /// streets, and what stands around the player is counted.
    /// </summary>
    public class TrafficNearPlayerTests
    {
        /// <summary>A place to stand: a name and WGS84.</summary>
        internal struct Spot
        {
            public string Name;
            public double Lon, Lat;
            public bool Busy;
        }

        internal static readonly Spot[] Spots =
        {
            new Spot { Name = "Ring Road, Chabahil", Lon = 85.3462, Lat = 27.7120 },
            new Spot { Name = "New Baneshwor", Lon = 85.3355, Lat = 27.6890 },
            new Spot { Name = "Kantipath", Lon = 85.3146, Lat = 27.7085 },
            new Spot { Name = "Thamel", Lon = 85.31230, Lat = 27.71530, Busy = true },
            new Spot { Name = "Asan", Lon = 85.31130, Lat = 27.70790, Busy = true },
            new Spot { Name = "New Road", Lon = 85.3095, Lat = 27.7035, Busy = true },
            new Spot { Name = "Baluwatar lanes", Lon = 85.3297, Lat = 27.7279 },
            new Spot { Name = "Bakhundol lanes", Lon = 85.3180, Lat = 27.6830 },
        };

        /// <summary>What the player saw in one run.</summary>
        internal struct Seen
        {
            /// <summary>Mean moving vehicles (full simulation, not ghosts) within 60 m and 120 m of the player.</summary>
            public double Mean60, Mean120;

            /// <summary>Share of the samples with at least one vehicle within 60 m, and with a moving two-wheeler within
            /// 70 m.</summary>
            public double Any60, Rider70;

            /// <summary>Mean two-wheelers within 70 m.</summary>
            public double Riders70;

            /// <summary>Samples of vehicles within 120 m, how many stood right of their lane's line (wrong side), how far
            /// they stood from it at most and on average, and how many stood still.</summary>
            public int Near, WrongSide, Stopped, StoppedAtJunction;

            public double MaxOff, MeanOff;
            public int Most120;

            /// <summary>The longest any vehicle within 120 m stood still, seconds.</summary>
            public double LongestStop;
        }

        /// <summary>The nearest road lane point to (x, z) (the player stands on a road).</summary>
        internal static void SnapToRoad(LaneGraph g, ref double x, ref double z)
        {
            LaneId lane;
            float s;
            if (!g.TryNearestLane(x, z, VehicleClass.TwoWheeler, 120.0, out lane, out s)) return;
            LaneInfo i = g.Info(lane);
            double best = double.MaxValue, bx = x, bz = z;
            for (int k = 0; k < i.PointCount; k++)
            {
                double dx = i.X[k] - x, dz = i.Z[k] - z, d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    bx = i.X[k];
                    bz = i.Z[k];
                }
            }
            x = bx;
            z = bz;
        }

        /// <summary>Signed distance of (x, z) from lane <paramref name="i"/>'s polyline: positive right of its direction.</summary>
        internal static double LaneOffset(in LaneInfo i, double x, double z)
        {
            double best = double.MaxValue, side = 0;
            for (int k = 0; k + 1 < i.PointCount; k++)
            {
                double ax = i.X[k], az = i.Z[k], dx = i.X[k + 1] - ax, dz = i.Z[k + 1] - az, l2 = dx * dx + dz * dz;
                double u = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                double qx = ax + dx * u - x, qz = az + dz * u - z, d = qx * qx + qz * qz;
                if (d < best)
                {
                    best = d;
                    // Right of travel (dx, dz) is (dz, -dx) in game X/Z.
                    side = (x - ax) * dz - (z - az) * dx >= 0 ? 1 : -1;
                }
            }
            return side * Math.Sqrt(best);
        }

        /// <summary>Runs the traffic at <paramref name="spot"/> for <paramref name="seconds"/> after a minute's warm-up, the
        /// player standing on the road looking north (then turning round every 20 s), and counts what is around.</summary>
        internal static Seen Run(Spot spot, TrafficSettings settings, ulong seed, float seconds = 180f)
        {
            LaneGraph g = TrafficData.Graph;
            double x, z;
            TrafficData.Game(new[] { spot.Lon, spot.Lat }, out x, out z);
            SnapToRoad(g, ref x, ref z);
            var sim = new TrafficSim(g, settings, seed) { Sacred = TrafficData.Zones };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(x, z, 0f);
            const float dt = 0.1f;
            var poses = new AgentPose[512];
            var seen = new Seen();
            var stoppedSince = new Dictionary<int, float>();
            int samples = 0;
            double offSum = 0;
            for (int step = 0; step < (int)((60f + seconds) / dt); step++)
            {
                float t = step * dt;
                // The camera looks north, then south, east, west in turn (spawns stay out of its view).
                int facing = (int)(t / 20f) % 4;
                sim.SetView(facing == 2 ? 1f : facing == 3 ? -1f : 0f, facing == 0 ? 1f : facing == 1 ? -1f : 0f);
                float hour = 8.5f + t / 3600f;
                bus.Step(dt, hour);
                sim.Step(dt, hour, 0f);
                if (t < 60f || step % 10 != 0) continue;
                samples++;
                int n = sim.CopyPoses(poses);
                int in60 = 0, in120 = 0, riders = 0;
                for (int k = 0; k < n; k++)
                {
                    AgentPose p = poses[k];
                    if ((p.AnimState & (byte)AgentAnim.Ghost) != 0) continue;
                    double dx = p.X - x, dz = p.Z - z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (d <= 60) in60++;
                    if (d <= 70 && (p.Class == VehicleClass.TwoWheeler || p.Class == VehicleClass.Bicycle)) riders++;
                    if (d > 120) continue;
                    in120++;
                    LaneId lane;
                    float s, speed;
                    if (!sim.TryGetAgent(p.AgentId, out lane, out s, out speed)) continue;
                    LaneInfo li = g.Info(lane);
                    if (li.Kind != LaneKind.Road) continue; // in a junction connector or a ring
                    seen.Near++;
                    if (Math.Abs(p.SpeedMps) >= 0.3f) stoppedSince.Remove(p.AgentId);
                    else
                    {
                        float since;
                        if (!stoppedSince.TryGetValue(p.AgentId, out since)) stoppedSince[p.AgentId] = since = t;
                        seen.LongestStop = Math.Max(seen.LongestStop, t - since);
                        seen.Stopped++;
                        if (li.ControllerId >= 0 && li.LengthM - s < 60f) seen.StoppedAtJunction++;
                    }
                    // The body's front axle point follows the lane; the pose is the rear axle: measure the pose against the
                    // lane, which it lies on within the steering and lateral-avoidance offsets.
                    double off = Math.Abs(LaneOffset(li, p.X, p.Z));
                    offSum += off;
                    seen.MaxOff = Math.Max(seen.MaxOff, off);
                    // Left-hand traffic: a two-way road's lane lies left of the road's centre in its travel direction.
                    if (li.TwoWay && (li.Forward ? li.OffsetM >= 0f : li.OffsetM <= 0f)) seen.WrongSide++;
                }
                seen.Mean60 += in60;
                seen.Mean120 += in120;
                seen.Most120 = Math.Max(seen.Most120, in120);
                seen.Riders70 += riders;
                if (in60 > 0) seen.Any60++;
                if (riders > 0) seen.Rider70++;
            }
            seen.Mean60 /= Math.Max(1, samples);
            seen.Mean120 /= Math.Max(1, samples);
            seen.Riders70 /= Math.Max(1, samples);
            seen.Any60 /= Math.Max(1, samples);
            seen.Rider70 /= Math.Max(1, samples);
            seen.MeanOff = offSum / Math.Max(1, seen.Near);
            return seen;
        }

        internal static TrafficSettings Tier(int tier)
        {
            return tier <= 0 ? TrafficSettings.Low() : tier == 1 ? TrafficSettings.Mid() : TrafficSettings.High();
        }

        internal static string Describe(Spot spot, int tier, in Seen s)
        {
            return string.Format("tier {0} {1,-20}: {2:0.0} within 60 m ({3:0}% of the time any), {4:0.0} within 120 m (most {5}), {6:0.0} riders within 70 m ({7:0}% of the time), " +
                                 "lane offset mean {8:0.00} max {9:0.0} m, wrong side {10}/{11}, stopped {12:0}% ({13:0}% queued at a junction), longest stop {14:0} s",
                                 tier, spot.Name, s.Mean60, 100 * s.Any60, s.Mean120, s.Most120, s.Riders70, 100 * s.Rider70, s.MeanOff, s.MaxOff, s.WrongSide, s.Near,
                                 100.0 * s.Stopped / Math.Max(1, s.Near), 100.0 * s.StoppedAtJunction / Math.Max(1, s.Near), s.LongestStop);
        }

        /// <summary>
        /// Playtest #6 ("absolutely no traffic on the road ... show a few that follow the lanes properly") at every tier:
        /// standing on a main road, a bazaar or a quiet side street, the player sees moving traffic round them (on average
        /// at least <see cref="MinMean120"/> within 120 m, and something within 60 m most of the time), every vehicle on a
        /// two-way road keeps to the LEFT lane of its direction, on its lane line (mean offset under 0.6 m), and nothing
        /// in sight is jammed (stands still longer than <see cref="MaxStopS"/>: a police red can hold a queue a minute). Playtest #3: in the busy bazaars a moving motorbike or scooter is in view
        /// (within 70 m) nearly all the time, two or three on average, instead of the parked ones.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TrafficIsSeenRoundThePlayerKeepingLeftAtEveryTier(int tier)
        {
            var sb = new StringBuilder();
            var fails = new List<string>();
            foreach (Spot spot in Spots)
            {
                Seen s = Run(spot, Tier(tier), 17, 120f);
                sb.AppendLine(Describe(spot, tier, s));
                if (s.Mean120 < MinMean120) fails.Add(spot.Name + ": only " + s.Mean120.ToString("0.0") + " vehicles within 120 m");
                if (s.Any60 < MinAny60) fails.Add(spot.Name + ": a vehicle within 60 m only " + (100 * s.Any60).ToString("0") + "% of the time");
                if (s.WrongSide > 0) fails.Add(spot.Name + ": " + s.WrongSide + " samples on the right-hand lane");
                if (s.MeanOff > 0.6) fails.Add(spot.Name + ": mean lane offset " + s.MeanOff.ToString("0.00") + " m");
                // A red at a police junction holds a queue a minute or more (Baneshwor); a vehicle standing the whole two
                // minutes is a jam.
                if (s.LongestStop > MaxStopS) fails.Add(spot.Name + ": a vehicle stood still " + s.LongestStop.ToString("0") + " s in sight");
                if (spot.Busy && (s.Rider70 < 0.9 || s.Riders70 < 1.5))
                    fails.Add(spot.Name + ": riders within 70 m " + s.Riders70.ToString("0.0") + " (" + (100 * s.Rider70).ToString("0") + "% of the time)");
            }
            TestContext.WriteLine(sb.ToString());
            Assert.That(fails, Is.Empty, string.Join("; ", fails));
        }

        /// <summary>Least mean count of moving vehicles within 120 m of the player.</summary>
        private const double MinMean120 = 4.0;

        /// <summary>Longest a vehicle within 120 m may stand still (the window is 120 s).</summary>
        private const double MaxStopS = 100.0;

        /// <summary>Least share of the time some vehicle is within 60 m.</summary>
        private const double MinAny60 = 0.6;

        /// <summary>Diagnostics: every spot on every tier.</summary>
        [Test, Explicit("diagnostics: prints the traffic around the player at every spot and tier")]
        public void ProbeTrafficAroundThePlayer()
        {
            var sb = new StringBuilder();
            for (int tier = 0; tier < 3; tier++)
                foreach (Spot spot in Spots)
                    sb.AppendLine(Describe(spot, tier, Run(spot, Tier(tier), 17, 120f)));
            TestContext.WriteLine(sb.ToString());
        }
    }
}
