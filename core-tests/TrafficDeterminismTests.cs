using System;
using System.IO;
using Ghumante.Core.Aviation;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V9: the same seed gives identical traffic, crowd and aircraft snapshots after ten minutes
    /// of headless simulation; a different seed does not.</summary>
    public class TrafficDeterminismTests
    {
        private const float Dt = 1f / 15f;

        private static ulong Hash(ulong h, double v)
        {
            h ^= (ulong)BitConverter.DoubleToInt64Bits(v);
            h *= 1099511628211UL;
            return h;
        }

        /// <summary>FNV-style hash of every published pose after ten minutes around Thamel and Boudha.</summary>
        private static ulong Snapshot(ulong seed)
        {
            LaneGraph g = TrafficData.Graph;
            double x, z, bx, bz;
            TrafficData.Game(TrafficData.RingRoadChabahil, out x, out z);
            TrafficData.Game(TrafficData.Boudha, out bx, out bz);
            var sim = new TrafficSim(g, TrafficSettings.Mid(), seed) { Sacred = TrafficData.Zones };
            var animals = new AnimalSim(g, AnimalSettings.Mid(), seed) { Traffic = sim };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            PedestrianSim peds = TrafficData.Pedestrians(PedestrianSettings.Mid(), seed, sim);
            var air = new AirTrafficSim(AviationConfig.Parse(File.ReadAllText(DrivingData.SamplePath(".aviation.json"))), seed);
            sim.SetFocus(x, z, 0f);
            animals.SetFocus(x, z, 0f);
            peds.SetFocus(bx, bz, 0f);
            for (int i = 0; i < 15 * 600; i++)
            {
                float hour = 8f + i * Dt / 120f; // one game hour per two real minutes
                animals.Step(Dt, hour, 0f);
                bus.Step(Dt, hour);
                sim.Step(Dt, hour, 0f);
                peds.Step(Dt, hour, 0f);
                air.Step(Dt, hour, 10, WeatherKind.Clear);
            }
            ulong h = 14695981039346656037UL;
            var poses = new AgentPose[512];
            int n = sim.CopyPoses(poses);
            Assert.That(n, Is.GreaterThan(5), "traffic");
            for (int k = 0; k < n; k++)
            {
                h = Hash(h, poses[k].X);
                h = Hash(h, poses[k].Z);
                h = Hash(h, poses[k].HeadingRad);
                h = Hash(h, poses[k].SpeedMps);
                h = Hash(h, poses[k].Variant);
                h = Hash(h, poses[k].AgentId);
            }
            var pp = new PedPose[512];
            int np = peds.CopyPoses(pp);
            Assert.That(np, Is.GreaterThan(5), "crowd");
            for (int k = 0; k < np; k++)
            {
                h = Hash(h, pp[k].X);
                h = Hash(h, pp[k].Z);
                h = Hash(h, pp[k].HeadingRad);
                h = Hash(h, pp[k].Archetype);
            }
            var ap = new AnimalPose[512];
            int na = animals.CopyPoses(ap);
            for (int k = 0; k < na; k++)
            {
                h = Hash(h, ap[k].X);
                h = Hash(h, ap[k].Z);
            }
            var st = new AircraftState[64];
            int ns = air.CopyStates(st);
            Assert.That(ns, Is.GreaterThan(0), "aircraft");
            for (int k = 0; k < ns; k++)
            {
                h = Hash(h, st[k].X);
                h = Hash(h, st[k].Y);
                h = Hash(h, st[k].Z);
                h = Hash(h, (double)st[k].Phase);
            }
            return h;
        }

        [Test]
        public void TheSameSeedGivesTheSameWorld()
        {
            ulong a = Snapshot(42), b = Snapshot(42), c = Snapshot(43);
            Assert.That(b, Is.EqualTo(a), "same seed, same snapshot");
            Assert.That(c, Is.Not.EqualTo(a), "the seed matters");
        }
    }
}
